#!/usr/bin/env bash
set -euo pipefail

# Lightweight latency smoke test for the running SmartTask API (handbook §17,
# Appendix G). Sequential curl requests, warm-up excluded, nearest-rank
# percentiles. Not a load test: it measures single-client latency only.
#
# Usage:
#   SMARTTASK_PERF_PASSWORD='...' scripts/perf-smoke.sh
#
# Environment:
#   SMARTTASK_API_URL        default https://localhost:7251
#   SMARTTASK_PERF_EMAIL     default demo@smarttask.local
#   SMARTTASK_PERF_PASSWORD  required (never hard-coded or printed)
#   PERF_ITERATIONS          measured requests per read endpoint, default 50
#   PERF_WARMUP              unmeasured warm-up requests per endpoint, default 3
#   PERF_LOGIN_ITERATIONS    measured logins, default 8 (auth limit is 10/min/IP)

BASE_URL="${SMARTTASK_API_URL:-https://localhost:7251}"
EMAIL="${SMARTTASK_PERF_EMAIL:-demo@smarttask.local}"
ITERATIONS="${PERF_ITERATIONS:-50}"
WARMUP="${PERF_WARMUP:-3}"
LOGIN_ITERATIONS="${PERF_LOGIN_ITERATIONS:-8}"

if [[ -z "${SMARTTASK_PERF_PASSWORD:-}" ]]; then
  echo "Set SMARTTASK_PERF_PASSWORD for $EMAIL." >&2
  exit 1
fi

if (( LOGIN_ITERATIONS > 9 )); then
  echo "PERF_LOGIN_ITERATIONS must be <= 9 (one more login is needed for the token)." >&2
  exit 1
fi

WORK_DIR="$(mktemp -d)"
trap 'rm -rf "$WORK_DIR"' EXIT
COOKIES="$WORK_DIR/cookies.txt"

# -k: the local development certificate may not be trusted by curl.
CURL=(curl -sk --max-time 30)

csrf_token="$("${CURL[@]}" -c "$COOKIES" -b "$COOKIES" "$BASE_URL/api/v1/auth/csrf" |
  python3 -c 'import json,sys; print(json.load(sys.stdin)["requestToken"])')"

login_body="$WORK_DIR/login.json"
python3 - "$EMAIL" > "$login_body" <<'PY'
import json, os, sys
print(json.dumps({"email": sys.argv[1], "password": os.environ["SMARTTASK_PERF_PASSWORD"]}))
PY

login_once() {
  "${CURL[@]}" -c "$COOKIES" -b "$COOKIES" \
    -H "Content-Type: application/json" \
    -H "X-CSRF-TOKEN: $csrf_token" \
    --data @"$login_body" \
    "$@" \
    "$BASE_URL/api/v1/auth/login"
}

# Prints "<http_code> <seconds>" per request into $1.
measure() {
  local out="$1" count="$2" warmup="$3"
  shift 3
  local i
  for ((i = 0; i < warmup; i++)); do
    "${CURL[@]}" -o /dev/null "$@" || true
  done
  : > "$out"
  for ((i = 0; i < count; i++)); do
    "${CURL[@]}" -o /dev/null -w '%{http_code} %{time_total}\n' "$@" >> "$out"
  done
}

summarize() {
  local name="$1" file="$2" target="$3"
  python3 - "$name" "$file" "$target" <<'PY'
import math, sys
name, path, target = sys.argv[1], sys.argv[2], sys.argv[3]
rows = [line.split() for line in open(path) if line.strip()]
codes = [int(r[0]) for r in rows]
ms = sorted(float(r[1]) * 1000 for r in rows)
def pct(p):
    return ms[max(0, math.ceil(p / 100 * len(ms)) - 1)]
errors = sum(1 for c in codes if not 200 <= c < 300)
p95 = pct(95)
verdict = "n/a"
if target != "-":
    verdict = "PASS" if p95 < float(target) and errors == 0 else "FAIL"
target_text = f"< {int(float(target))} ms" if target != "-" else "-"
print(f"| {name} | {len(ms)} | {errors} | {ms[0]:.1f} | {pct(50):.1f} | {p95:.1f} | {ms[-1]:.1f} | {target_text} | {verdict} |")
PY
}

auth_header() { printf 'Authorization: Bearer %s' "$ACCESS_TOKEN"; }

# Login: LOGIN_ITERATIONS measured requests, then one more to obtain the token.
: > "$WORK_DIR/login.txt"
for ((i = 0; i < LOGIN_ITERATIONS; i++)); do
  login_once -o /dev/null -w '%{http_code} %{time_total}\n' >> "$WORK_DIR/login.txt"
done

ACCESS_TOKEN="$(login_once | python3 -c 'import json,sys; print(json.load(sys.stdin)["accessToken"])')"

project_id="$("${CURL[@]}" -H "$(auth_header)" "$BASE_URL/api/v1/projects?pageSize=1" |
  python3 -c 'import json,sys; items=json.load(sys.stdin).get("items", []); print(items[0]["id"] if items else "")')"

measure "$WORK_DIR/health.txt" "$ITERATIONS" "$WARMUP" "$BASE_URL/health"
measure "$WORK_DIR/projects.txt" "$ITERATIONS" "$WARMUP" -H "$(auth_header)" \
  "$BASE_URL/api/v1/projects?pageNumber=1&pageSize=20"
measure "$WORK_DIR/projects-search.txt" "$ITERATIONS" "$WARMUP" -H "$(auth_header)" \
  "$BASE_URL/api/v1/projects?search=a&sortBy=name&pageSize=20"
measure "$WORK_DIR/summary.txt" "$ITERATIONS" "$WARMUP" -H "$(auth_header)" \
  "$BASE_URL/api/v1/dashboard/summary"
measure "$WORK_DIR/statistics.txt" "$ITERATIONS" "$WARMUP" -H "$(auth_header)" \
  "$BASE_URL/api/v1/dashboard/task-statistics"

if [[ -n "$project_id" ]]; then
  measure "$WORK_DIR/tasks.txt" "$ITERATIONS" "$WARMUP" -H "$(auth_header)" \
    "$BASE_URL/api/v1/projects/$project_id/tasks?pageNumber=1&pageSize=20"
  measure "$WORK_DIR/tasks-search.txt" "$ITERATIONS" "$WARMUP" -H "$(auth_header)" \
    "$BASE_URL/api/v1/projects/$project_id/tasks?search=a&sortBy=dueDate&pageSize=20"
fi

echo "Base URL: $BASE_URL | iterations: $ITERATIONS (+$WARMUP warm-up) | logins: $LOGIN_ITERATIONS | $(date -u '+%Y-%m-%dT%H:%M:%SZ')"
echo
echo "| Endpoint | n | Non-2xx | Min ms | p50 ms | p95 ms | Max ms | Target (p95) | Result |"
echo "|---|---|---|---|---|---|---|---|---|"
summarize "GET /health" "$WORK_DIR/health.txt" "-"
summarize "POST /api/v1/auth/login" "$WORK_DIR/login.txt" 1000
summarize "GET /api/v1/projects (page 1, size 20)" "$WORK_DIR/projects.txt" 500
summarize "GET /api/v1/projects?search=a" "$WORK_DIR/projects-search.txt" 500
if [[ -n "$project_id" ]]; then
  summarize "GET /api/v1/projects/{id}/tasks (page 1, size 20)" "$WORK_DIR/tasks.txt" 500
  summarize "GET /api/v1/projects/{id}/tasks?search=a" "$WORK_DIR/tasks-search.txt" 500
else
  echo "| GET /api/v1/projects/{id}/tasks | 0 | - | - | - | - | - | < 500 ms | skipped (user has no projects) |"
fi
summarize "GET /api/v1/dashboard/summary" "$WORK_DIR/summary.txt" 800
summarize "GET /api/v1/dashboard/task-statistics" "$WORK_DIR/statistics.txt" 800
