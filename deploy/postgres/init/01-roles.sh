#!/bin/sh
# Runs once on the first start of the postgres container. Three accounts, three jobs:
#   clinic_owner  owns schema "clinic" (the synthetic source system; loads data, grants read access)
#   kvkk_scanner  read-only: catalog + SELECT on what it is granted, nothing else
#   kvkk_masker   reads the source, owns schema "clinic_masked" where the masked copy is written
set -eu
: "${OWNER_PASSWORD:?OWNER_PASSWORD must be set}"
: "${SCANNER_PASSWORD:?SCANNER_PASSWORD must be set}"
: "${MASKER_PASSWORD:?MASKER_PASSWORD must be set}"

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<SQL
CREATE ROLE clinic_owner LOGIN PASSWORD '${OWNER_PASSWORD}';
CREATE ROLE kvkk_read NOLOGIN;
CREATE ROLE kvkk_scanner LOGIN PASSWORD '${SCANNER_PASSWORD}' IN ROLE kvkk_read;
CREATE ROLE kvkk_masker LOGIN PASSWORD '${MASKER_PASSWORD}' IN ROLE kvkk_read;

REVOKE ALL ON SCHEMA public FROM PUBLIC;
REVOKE CREATE ON DATABASE "${POSTGRES_DB}" FROM PUBLIC;

CREATE SCHEMA clinic AUTHORIZATION clinic_owner;
GRANT USAGE ON SCHEMA clinic TO kvkk_read;
CREATE SCHEMA clinic_masked AUTHORIZATION kvkk_masker;
SQL
