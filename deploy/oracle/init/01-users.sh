#!/bin/bash
# Runs once on the first start of the gvenzl/oracle-free container, as the oracle OS user.
#   CLINIC         owns the synthetic source tables; grants SELECT on them to KVKK_READ
#   KVKK_SCANNER   read-only: CREATE SESSION + KVKK_READ, nothing else
#   KVKK_MASKED    reads the source through KVKK_READ, creates the masked copy in its own schema only
set -euo pipefail
: "${OWNER_PASSWORD:?OWNER_PASSWORD must be set}"
: "${SCANNER_PASSWORD:?SCANNER_PASSWORD must be set}"
: "${MASKER_PASSWORD:?MASKER_PASSWORD must be set}"

sqlplus -s / as sysdba <<SQL
WHENEVER SQLERROR EXIT SQL.SQLCODE
ALTER SESSION SET CONTAINER = FREEPDB1;

CREATE ROLE kvkk_read;

CREATE USER clinic IDENTIFIED BY "${OWNER_PASSWORD}" DEFAULT TABLESPACE users QUOTA 1G ON users;
GRANT CREATE SESSION, CREATE TABLE TO clinic;

CREATE USER kvkk_scanner IDENTIFIED BY "${SCANNER_PASSWORD}";
GRANT CREATE SESSION, kvkk_read TO kvkk_scanner;

CREATE USER kvkk_masked IDENTIFIED BY "${MASKER_PASSWORD}" DEFAULT TABLESPACE users QUOTA 1G ON users;
GRANT CREATE SESSION, CREATE TABLE, kvkk_read TO kvkk_masked;
EXIT
SQL
