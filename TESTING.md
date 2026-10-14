# Scénarios de test

Ce document liste des scénarios reproductibles pour vérifier la solution de bout en bout.
Chaque scénario indique son objectif, les commandes à exécuter et le résultat attendu.

## Prérequis

```bash
cd /Users/antoine/DEV/EntretienTec
docker compose up -d postgres        # PostgreSQL (base fuelmix + rôles)
```

Rappel des variables d'environnement locales (mode `dotnet run`) :

```bash
# Worker (ingestion)
MIGRATOR="Host=localhost;Port=5432;Database=fuelmix;Username=fuelmix_migrator;Password=migrator"
INGEST="Host=localhost;Port=5432;Database=fuelmix;Username=fuelmix_ingest;Password=ingest"
# API
REPORT="Host=localhost;Port=5432;Database=fuelmix;Username=fuelmix_report;Password=report"
APIKEY="ma-cle-de-test"
```

---

## Scénario 1 — Ingestion ponctuelle (live)

**Objectif** : importer le snapshot live MISO et vérifier les données en base.

```bash
ConnectionStrings__Migrator="$MIGRATOR" ConnectionStrings__Ingest="$INGEST" \
dotnet run --project src/FuelMix.Worker -- --once live
```

**Attendu** : `Ingestion success … received=8 inserted=8 updated=0 unchanged=0 skipped=0`.

Vérification :

```bash
docker exec -it fuelmix-postgres-1 psql -U fuelmix_report -d fuelmix \
  -c "SELECT interval_utc, category, megawatts FROM fuelmix.fuel_mix ORDER BY category;"
```

**Attendu** : 8 lignes (8 catégories), valeurs négatives possibles (Battery Storage, Imports).

---

## Scénario 2 — Idempotence (re-exécution)

**Objectif** : rejouer le même import ne crée **aucun doublon**.

```bash
ConnectionStrings__Migrator="$MIGRATOR" ConnectionStrings__Ingest="$INGEST" \
dotnet run --project src/FuelMix.Worker -- --once live
```

**Attendu** : `inserted=0 unchanged=8` (si l'intervalle MISO n'a pas changé) et le total de lignes reste stable :

```bash
docker exec -it fuelmix-postgres-1 psql -U fuelmix_report -d fuelmix \
  -c "SELECT count(*), count(DISTINCT (interval_utc, category)) FROM fuelmix.fuel_mix;"
```

**Attendu** : `count = count distinct` (aucun doublon).

---

## Scénario 3 — Backfill historique (today / yesterday)

**Objectif** : importer un historique complet pour la démonstration.

```bash
ConnectionStrings__Migrator="$MIGRATOR" ConnectionStrings__Ingest="$INGEST" \
dotnet run --project src/FuelMix.Worker -- --once today

ConnectionStrings__Migrator="$MIGRATOR" ConnectionStrings__Ingest="$INGEST" \
dotnet run --project src/FuelMix.Worker -- --once yesterday
```

**Attendu** : `received` élevé (plusieurs centaines), `skipped=0` (toutes les heures, y compris à 1 chiffre, sont parsées).

---

## Scénario 4 — Deux imports concurrents (pas de doublon)

**Objectif** : deux imports simultanés du même intervalle ne créent pas de doublon.

```bash
DLL=src/FuelMix.Worker/bin/Debug/net10.0/FuelMix.Worker.dll
ConnectionStrings__Migrator="$MIGRATOR" ConnectionStrings__Ingest="$INGEST" dotnet "$DLL" --once live > /tmp/a.log 2>&1 &
ConnectionStrings__Migrator="$MIGRATOR" ConnectionStrings__Ingest="$INGEST" dotnet "$DLL" --once live > /tmp/b.log 2>&1 &
wait
grep Ingestion /tmp/a.log /tmp/b.log
```

**Attendu** : l'un insère (`inserted=8`), l'autre `unchanged=8` ; `count = count distinct` en base.

---

## Scénario 5 — API : santé et lecture

**Objectif** : vérifier les endpoints de lecture.

Lancer l'API (terminal 1) :

```bash
ConnectionStrings__Report="$REPORT" Auth__ApiKey="$APIKEY" \
dotnet run --project src/FuelMix.Api
```

Dans un second terminal :

```bash
curl http://localhost:5111/health
curl -H "X-Api-Key: $APIKEY" http://localhost:5111/api/fuelmix/latest
curl -H "X-Api-Key: $APIKEY" "http://localhost:5111/api/fuelmix?category=Coal&pageSize=5"
curl -H "X-Api-Key: $APIKEY" "http://localhost:5111/api/fuelmix?from=2026-10-10T00:00:00Z&to=2026-10-12T00:00:00Z"
```

**Attendu** :
- `/health` → `200 {"status":"Healthy"}`.
- `/latest` → JSON avec `intervalUtc` au format ISO 8601 UTC (`Z`) et 8 entrées `fuels`.
- série temporelle → items triés par `intervalUtc` décroissant, `totalCount` cohérent.

---

## Scénario 6 — Sécurité : clé API, validation, pagination

**Objectif** : vérifier les contrôles de sécurité.

```bash
# Sans clé -> 401
curl -o /dev/null -w "%{http_code}\n" http://localhost:5111/api/fuelmix/latest

# Mauvaise clé -> 401
curl -o /dev/null -w "%{http_code}\n" -H "X-Api-Key: mauvaise" http://localhost:5111/api/fuelmix/latest

# from invalide -> 400
curl -o /dev/null -w "%{http_code}\n" -H "X-Api-Key: $APIKEY" "http://localhost:5111/api/fuelmix?from=pas-une-date"

# from > to -> 400
curl -o /dev/null -w "%{http_code}\n" -H "X-Api-Key: $APIKEY" "http://localhost:5111/api/fuelmix?from=2026-10-12T00:00:00Z&to=2026-10-10T00:00:00Z"

# pageSize énorme -> plafonné à 1000
curl -sS -H "X-Api-Key: $APIKEY" "http://localhost:5111/api/fuelmix?pageSize=99999" | head -c 300
```

**Attendu** : `401`, `401`, `400`, `400`, et `"pageSize":1000` dans la réponse.

---

## Scénario 7 — Lecture seule (rôle report)

**Objectif** : confirmer que le compte de reporting ne peut pas écrire.

```bash
docker exec -it fuelmix-postgres-1 psql -U fuelmix_report -d fuelmix \
  -c "INSERT INTO fuelmix.fuel_mix (interval_utc, category, megawatts, updated_at) VALUES (now(),'Coal',1,now());"
```

**Attendu** : `ERROR: permission denied` (table ou schéma).

---

## Scénario 8 — Mode périodique

**Objectif** : vérifier l'ingestion automatique à intervalle régulier.

```bash
ConnectionStrings__Migrator="$MIGRATOR" ConnectionStrings__Ingest="$INGEST" Ingest__IntervalSeconds=5 \
dotnet run --project src/FuelMix.Worker
```

**Attendu** : une ligne `Ingestion success …` toutes les ~5 s. Arrêter avec `Ctrl+C`.

---

## Scénario 9 — Résilience HTTP (429 / retry)

**Objectif** : vérifier la gestion du rate-limit (couvert par les tests unitaires, plus simple qu'en réel).

```bash
docker compose up -d postgres
dotnet test FuelMix.slnx --filter "FullyQualifiedName~MisoClientTests"
```

**Attendu** : tests verts couvrant 429, 500, 404, tentatives épuisées, `Retry-After` (secondes et date HTTP), annulation et budget.

---

## Scénario 10 — Déploiement conteneurisé complet

**Objectif** : tout exécuter en conteneurs (worker + api + postgres).

```bash
docker compose up --build -d
curl http://localhost:8080/health
curl -H "X-Api-Key: dev-api-key" http://localhost:8080/api/fuelmix/latest
docker compose logs -f worker     # suivre l'ingestion périodique
docker compose down               # arrêter (garde les données)
```

**Attendu** : `/health` → 200 ; `/latest` → 200 avec `dev-api-key` ; logs du worker visibles.

---

## Scénario 11 — Suite de tests complète

**Objectif** : exécuter l'ensemble des tests (unitaires + intégration PostgreSQL).

```bash
docker compose up -d postgres
dotnet test FuelMix.slnx
```

**Attendu** : `30/30 réussite, 0 échec, 0 ignoré`.
