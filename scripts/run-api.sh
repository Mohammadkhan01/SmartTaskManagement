#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ENV_FILE="$ROOT_DIR/docker/.env"
PROJECT="$ROOT_DIR/src/SmartTask.API/SmartTask.API.csproj"

if [[ ! -f "$ENV_FILE" ]]; then
  echo "Missing environment file: $ENV_FILE" >&2
  exit 1
fi

if [[ ! -f "$PROJECT" ]]; then
  echo "API project not found: $PROJECT" >&2
  exit 1
fi

while IFS= read -r line || [[ -n "$line" ]]; do
  [[ -z "$line" || "$line" == \\#* || "$line" != *=* ]] && continue
  key="${line%%=*}"
  value="${line#*=}"
  export "$key=$value"
done < "$ENV_FILE"

cd "$ROOT_DIR"
exec dotnet run --no-launch-profile --project "$PROJECT"
