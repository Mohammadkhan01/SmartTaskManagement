#!/usr/bin/env bash
set -euo pipefail

# Migrates SmartTaskTestDb and runs the SQL Server integration tests.
# SMARTTASK_TEST_CONNECTION_STRING is used when already exported; otherwise it
# is built from MSSQL_SA_PASSWORD in the git-ignored docker/.env file.

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ENV_FILE="$ROOT_DIR/docker/.env"

if [[ -z "${SMARTTASK_TEST_CONNECTION_STRING:-}" ]]; then
  if [[ ! -f "$ENV_FILE" ]]; then
    echo "Set SMARTTASK_TEST_CONNECTION_STRING or create $ENV_FILE." >&2
    exit 1
  fi

  password="$(sed -n 's/^MSSQL_SA_PASSWORD=//p' "$ENV_FILE" | head -n 1)"

  if [[ -z "$password" ]]; then
    echo "MSSQL_SA_PASSWORD is missing from $ENV_FILE." >&2
    exit 1
  fi

  export SMARTTASK_TEST_CONNECTION_STRING="Server=${SMARTTASK_TEST_SQL_SERVER:-localhost,1433};Database=SmartTaskTestDb;User Id=sa;Password=${password};TrustServerCertificate=True;Encrypt=True"
  unset password
fi

cd "$ROOT_DIR"

# EF creates the database when it does not exist and applies pending migrations.
SMARTTASK_DESIGN_TIME_CONNECTION_STRING="$SMARTTASK_TEST_CONNECTION_STRING" \
  dotnet ef database update \
    --project src/SmartTask.DAL \
    --startup-project src/SmartTask.DAL

dotnet test tests/SmartTask.IntegrationTests -nodeReuse:false "$@"
