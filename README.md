# Fuel Mix ELT — MISO

Service backend **ELT** qui importe périodiquement le mix énergétique de l'API publique
[MISO](https://public-api.misoenergy.org/api/FuelMix), le charge dans **PostgreSQL**, et
l'expose en **lecture seule** via une API REST protégée par clé API.

## Aperçu

```
MISO API ──(HTTP, retry/backoff)──▶ Worker .NET (ingestion périodique / --once)
                                        │  upsert idempotent (transaction)
                                        ▼
                                   PostgreSQL (schéma `fuelmix`)
                                        │  lecture seule (rôle report)
                                        ▼
                            API ASP.NET Core ──(X-Api-Key)──▶ utilisateur externe
```

| Composant | Projet | Rôle |
|---|---|---|
| Ingestion | `src/FuelMix.Worker` | BackgroundService (périodique) + mode `--once` |
| API | `src/FuelMix.Api` | Lecture seule, clé API, pagination |
| Partagé | `src/FuelMix.Infrastructure` | Entités, DbContext, client MISO, upsert |
| Tests | `tests/FuelMix.Tests` | xUnit (unitaires + intégration) |

## Prérequis

- **Docker** (et Docker Compose) — suffit pour tout démarrer en conteneurs.
- .NET 10 SDK — uniquement pour le développement local et les tests.

## Démarrage rapide (tout en conteneurs)

Une seule commande démarre **PostgreSQL + le Worker (ingestion) + l'API** :

```bash
git clone <url-du-repo>
cd <nom-du-repo>
cp .env.example .env        # optionnel : des valeurs de démo par défaut sont déjà prévues
docker compose up --build
```

Cela :
- crée la base `fuelmix` et trois rôles applicatifs (moindre privilège) — voir tableau ci-dessous ;
- démarre le **Worker**, qui applique les migrations puis ingère le snapshot live (puis toutes les 300 s) ;
- démarre l'**API** sur le port `8080`.

### Tester l'API

```bash
curl http://localhost:8080/health
curl -H "X-Api-Key: dev-api-key" http://localhost:8080/api/fuelmix/latest
curl -H "X-Api-Key: dev-api-key" "http://localhost:8080/api/fuelmix?category=Coal&pageSize=5"
```

| Méthode | Chemin | Description |
|---|---|---|
| GET | `/health` | Liveness (non authentifié) |
| GET | `/api/fuelmix/latest` | Dernier instantané (8 catégories) |
| GET | `/api/fuelmix?from=&to=&category=&page=&pageSize=` | Série temporelle paginée |

### Rôles PostgreSQL (créés automatiquement)

| Rôle | Privilèges |
|---|---|
| `fuelmix_migrator` | propriétaire du schéma, exécute les migrations (DDL) |
| `fuelmix_ingest` | SELECT/INSERT/UPDATE sur les tables (aucun DDL) |
| `fuelmix_report` | SELECT uniquement (lecture seule) |

### Arrêter

```bash
docker compose down        # garde les données
# docker compose down -v   # efface aussi la base
```

## Développement local (sans Docker, optionnel)

Pour exécuter les composants individuellement (Worker en `--once`, API avec variables
d'environnement) ou lancer les tests, voir [TESTING.md](TESTING.md) (scénarios 1 à 11).

## Tests

```bash
# PostgreSQL doit être démarré (les tests d'intégration utilisent une base fuelmix_test)
docker compose up -d postgres
dotnet test FuelMix.slnx
```

- **Tests unitaires** : conversion des dates (dont DST), mapping JSON, client MISO (retry/429).
- **Tests d'intégration** : idempotence de l'upsert, annulation transactionnelle, isolation des rôles
  (contre un vrai PostgreSQL, base `fuelmix_test` recréée à chaque exécution).

Des scénarios de test détaillés et reproductibles sont décrits dans [TESTING.md](TESTING.md).

## Modèle de données (idempotence)

```sql
-- Schéma `fuelmix`
fuel_mix  (interval_utc timestamptz, category varchar(64), megawatts numeric(10,3), updated_at timestamptz)
          PRIMARY KEY (interval_utc, category)
          INDEX (category, interval_utc)

import_run (id bigint, started_at, finished_at, status, source,
            rows_received, rows_inserted, rows_updated, rows_unchanged, rows_skipped, error)
```

- **Clé métier** = `(interval_utc, category)` : garante de l'idempotence via la contrainte unique.
- **Upsert** : `INSERT … ON CONFLICT (interval_utc, category) DO UPDATE … WHERE megawatts IS DISTINCT FROM …`
  → aucune écriture si la valeur est inchangée. `RETURNING (xmax = 0)` distingue insertion, mise à jour
  et absence d'écriture, ce qui permet des compteurs exacts.
- **Valeurs négatives** autorisées (`Battery Storage`, `Imports`).
- **Fraîcheur** : « dernier import réussi » = `max(finished_at)` dans `import_run` ; « fraîcheur des
  données » = `max(interval_utc)` dans `fuel_mix`. Les deux notions sont distinctes (pas d'horodatage
  d'observation par mesure, pour éviter l'écriture systématique).

## Gestion des dates (DST)

`INTERVALEST` est une heure murale **America/New_York** (format 12 h, sans offset). La conversion
est **DST-aware** (jamais de décalage fixe) :

- heure **inexistante** (gap du passage à l'heure d'été) → mesure ignorée + loguée ;
- heure **ambiguë** (chevauchenent du retour à l'heure standard) → mesure ignorée + loguée.

La source ne fournissant pas d'offset, on refuse de deviner : une mesure ambiguë est **signalée et
ignorée** plutôt que fusionnée à tort. Le stockage est en `timestamptz` (UTC), exposé en ISO 8601 UTC
(`Z`) par l'API.

## Sécurité

- **DB jamais exposée** : l'API ne parle qu'à PostgreSQL via le rôle `fuelmix_report` (SELECT seul).
- **Clé API** : en-tête `X-Api-Key`, comparaison à temps constant, configurée par environnement
  (`Auth:ApiKey`), jamais codée en dur.
- **Paramètres validés** : dates ISO 8601, cohérence `from ≤ to`.
- **Pagination plafonnée** : `pageSize ∈ [1, 1000]`.
- **Logs sans secrets** : Serilog journalise méthode/chemin/statut, sans corps ni identifiants.
- **Secrets** : via variables d'environnement / `.env` (jamais committés).

## Résilience (rate-limit MISO)

Le client MISO gère le rate-limit sans dépendance externe :

- retry sur `429`, `408`, `5xx` et erreurs réseau transitoires ;
- **respect strict de `Retry-After`** (secondes ou date HTTP), jamais raccourci ;
- sinon backoff exponentiel avec jitter ;
- `4xx` non récupérables → échec immédiat ;
- nombre de tentatives, délai de base et **budget d'exécution** configurables (`Miso:*`) ;
- si un `Retry-After` dépasse le budget, l'import abandonne proprement (une exécution ultérieure reprend).

L'échec final est tracé dans `import_run` (status `failed`) puis l'exception est propagée à
l'orchestrateur. Aucun retry imbriqué entre le client et le service d'ingestion.

## Monitoring & alerting

- **Logs structurés** Serilog (console en local, prêts pour un sink CloudWatch).
- **Health check** : `GET /health` (connectivité base).
- **Métrique clé** : la fraîcheur des données (`max(interval_utc)` de `fuel_mix` vs l'heure courante).
- **Alerte recommandée (AWS)** : alarme CloudWatch « aucun intervalle ingéré depuis X minutes »,
  branchée sur une requête de fraîcheur ou sur `import_run`, notifiée via SNS (email).

## Infrastructure (bonus — Terraform)

L'infrastructure cible est décrite mais **non déployée** pour cette évaluation (compte gratuit).
Configuration Terraform prévue : VPC privée, RDS PostgreSQL (`db.t3.micro` free-tier, non public),
Secrets Manager pour les identifiants, ECS/Fargate (ou Lambda + EventBridge pour minimiser les coûts)
pour l'ingestion, API via Application Load Balancer, alarmes CloudWatch + SNS. Le déploiement est
idempotent (`terraform apply` répétable).

## Décisions de conception

- **.NET 10** (ASP.NET Core + EF Core + xUnit) et **Npgsql** pour PostgreSQL.
- **Un seul projet de tests** (unitaires + intégration), pas de SQLite in-memory pour prouver
  l'idempotence : les tests d'idempotence s'exécutent contre un vrai PostgreSQL.
- **Pas de Polly ni de bibliothèque superflue** : le retry est une boucle simple et testable
  (abstraction de délai injectée).
- **Modèle normalisé minimal** (une table de faits + une table de trace), adapté à un petit volume
  (~2 300 lignes/jour).
