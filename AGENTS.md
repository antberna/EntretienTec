# AGENTS.md

## Projet
Service ELT backend qui importe périodiquement le mix énergétique MISO (fuel mix)
et l'expose en lecture seule via une API REST.

## Stack
- .NET 10 — ASP.NET Core (API) + Worker (BackgroundService)
- EF Core + Npgsql (PostgreSQL 16)
- xUnit, Serilog
- Docker / Docker Compose (développement local)

## Conventions
- **Dates** : stockage en `timestamptz` UTC ; conversion de `INTERVALEST` depuis
  `America/New_York` (DST-aware, jamais de décalage fixe). Exposition ISO 8601 UTC.
- **Modèle** : `fuel_mix (interval_utc, category, megawatts, updated_at)` clé composite
  `(interval_utc, category)` + `import_run` (suivi des exécutions).
- **Idempotence** : `INSERT … ON CONFLICT DO UPDATE … WHERE megawatts IS DISTINCT FROM …`.
- **Rôles PostgreSQL** : `fuelmix_migrator` (DDL), `fuelmix_ingest` (écriture),
  `fuelmix_report` (SELECT uniquement).
- **Secrets** : jamais committés ; via variables d'environnement / `.env` (voir `.env.example`).

## Commandes de validation
```bash
dotnet build FuelMix.slnx
dotnet test FuelMix.slnx
docker compose up -d postgres      # puis attendre le healthcheck
docker compose ps
docker compose exec postgres psql -U fuelmix_report -d fuelmix -c 'SELECT 1'
```

## Étapes
1. Infra partagée (entités, DbContext, client MISO)  2. Ingestion (Worker)
3. API (lecture seule + clé API)  4. Tests d'intégration PostgreSQL  5. Observabilité
6. (Bonus) Terraform
