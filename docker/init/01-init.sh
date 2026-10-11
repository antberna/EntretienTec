#!/bin/bash
# Initialisation PostgreSQL (exécuté UNE fois, à la création du volume).
# Crée les rôles applicatifs (moindre privilège) et le schéma `fuelmix`.
# Les mots de passe proviennent de l'environnement du conteneur (jamais committés).
set -euo pipefail

MIGRATOR_PASSWORD="${FUELMIX_MIGRATOR_PASSWORD:-migrator}"
INGEST_PASSWORD="${FUELMIX_INGEST_PASSWORD:-ingest}"
REPORT_PASSWORD="${FUELMIX_REPORT_PASSWORD:-report}"

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<SQL
-- Rôles applicatifs
CREATE ROLE fuelmix_migrator LOGIN PASSWORD '${MIGRATOR_PASSWORD}';
CREATE ROLE fuelmix_ingest   LOGIN PASSWORD '${INGEST_PASSWORD}';
CREATE ROLE fuelmix_report   LOGIN PASSWORD '${REPORT_PASSWORD}';

-- Schéma dédié, détenu par le rôle de migration (évite d'écrire dans `public`)
CREATE SCHEMA fuelmix AUTHORIZATION fuelmix_migrator;

GRANT USAGE ON SCHEMA fuelmix TO fuelmix_ingest, fuelmix_report;

-- Privilèges par défaut : toute table/séquence créée par fuelmix_migrator
-- sera automatiquement accessible selon le moindre privilège.
ALTER DEFAULT PRIVILEGES FOR ROLE fuelmix_migrator IN SCHEMA fuelmix
    GRANT SELECT ON TABLES TO fuelmix_report;

ALTER DEFAULT PRIVILEGES FOR ROLE fuelmix_migrator IN SCHEMA fuelmix
    GRANT SELECT, INSERT, UPDATE ON TABLES TO fuelmix_ingest;

ALTER DEFAULT PRIVILEGES FOR ROLE fuelmix_migrator IN SCHEMA fuelmix
    GRANT USAGE, SELECT ON SEQUENCES TO fuelmix_ingest;
SQL
