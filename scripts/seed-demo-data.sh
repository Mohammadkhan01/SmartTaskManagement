#!/usr/bin/env bash
# Seed realistic demo users, projects, and tasks into a running SmartTask API.
#
# Usage:
#   SMARTTASK_ADMIN_PASSWORD='...' SMARTTASK_DEMO_PASSWORD='...' \
#     scripts/seed-demo-data.sh
#
# Environment:
#   SMARTTASK_API_URL          default https://localhost:7251
#   SMARTTASK_ADMIN_EMAIL      default e2e-admin@smarttask.local
#   SMARTTASK_ADMIN_PASSWORD   required (admin login; never printed)
#   SMARTTASK_DEMO_PASSWORD    required (password for newly registered demo users)
#   SMARTTASK_AUTH_DELAY_SEC   delay between auth calls, default 7 (limit is 10/min/IP)
#   SMARTTASK_CURL_INSECURE    set to 0 to disable curl -k (default 1 for local certs)
set -euo pipefail

BASE_URL="${SMARTTASK_API_URL:-https://localhost:7251}"
ADMIN_EMAIL="${SMARTTASK_ADMIN_EMAIL:-e2e-admin@smarttask.local}"
AUTH_DELAY_SEC="${SMARTTASK_AUTH_DELAY_SEC:-7}"
CURL_INSECURE="${SMARTTASK_CURL_INSECURE:-1}"

if [[ -z "${SMARTTASK_ADMIN_PASSWORD:-}" ]]; then
  echo "Set SMARTTASK_ADMIN_PASSWORD for $ADMIN_EMAIL." >&2
  exit 1
fi

if [[ -z "${SMARTTASK_DEMO_PASSWORD:-}" ]]; then
  echo "Set SMARTTASK_DEMO_PASSWORD for newly registered demo users." >&2
  exit 1
fi

WORK_DIR="$(mktemp -d)"
trap 'rm -rf "$WORK_DIR"' EXIT
COOKIES="$WORK_DIR/cookies.txt"
STATE="$WORK_DIR/state.json"

CURL=(curl -sS --max-time 60)
if [[ "$CURL_INSECURE" == "1" ]]; then
  CURL+=(-k)
fi

log() { printf '%s\n' "$*"; }
fail() { printf 'ERROR: %s\n' "$*" >&2; exit 1; }

auth_pause() {
  sleep "$AUTH_DELAY_SEC"
}

json_get() {
  python3 -c 'import json,sys; d=json.load(sys.stdin); print(d'"$1"')'
}

# Refresh CSRF cookie+token into COOKIES / CSRF_TOKEN.
refresh_csrf() {
  CSRF_TOKEN="$("${CURL[@]}" -c "$COOKIES" -b "$COOKIES" "$BASE_URL/api/v1/auth/csrf" |
    python3 -c 'import json,sys; print(json.load(sys.stdin)["requestToken"])')"
}

# Auth POST (register/login). Writes body to stdout and HTTP code to $WORK_DIR/auth_http_code.
# Callers must not capture via $(...) if they need the code in-shell; use a redirect instead.
auth_post() {
  local path="$1"
  local body_file="$2"
  local out_file="$WORK_DIR/auth_out.json"
  local code
  code="$("${CURL[@]}" -c "$COOKIES" -b "$COOKIES" \
    -o "$out_file" -w '%{http_code}' \
    -H "Content-Type: application/json" \
    -H "X-CSRF-TOKEN: $CSRF_TOKEN" \
    --data @"$body_file" \
    "$BASE_URL$path")"
  printf '%s' "$code" > "$WORK_DIR/auth_http_code"
  cat "$out_file"
}

# Authenticated API call. Args after method+path are optional curl extras (e.g. --data).
api() {
  local method="$1"
  local path="$2"
  shift 2
  local out_file="$WORK_DIR/api_out.json"
  local code
  code="$("${CURL[@]}" -o "$out_file" -w '%{http_code}' \
    -X "$method" \
    -H "Authorization: Bearer $ACCESS_TOKEN" \
    -H "Content-Type: application/json" \
    -H "Accept: application/json" \
    "$@" \
    "$BASE_URL$path")"
  if [[ "$code" -ge 400 ]]; then
    printf 'API %s %s -> %s\n%s\n' "$method" "$path" "$code" "$(cat "$out_file")" >&2
    return 1
  fi
  cat "$out_file"
}

log "Seeding SmartTask demo data against $BASE_URL"

# --- Register demo personas (rate-limited) ------------------------------------
# Role applied after admin login. Existing emails are skipped.
DEMO_USERS_SPEC=$(cat <<'EOF'
Sarah Chen|sarah.chen@example.com|Project Manager
James Okonkwo|james.okonkwo@example.com|Project Manager
Priya Sharma|priya.sharma@example.com|Team Member
Marcus Webb|marcus.webb@example.com|Team Member
Elena Rossi|elena.rossi@example.com|Team Member
EOF
)

REGISTERED=0
SKIPPED_EXISTING=0
while IFS='|' read -r display_name email desired_role; do
  [[ -z "${email:-}" ]] && continue
  refresh_csrf
  python3 - "$display_name" "$email" > "$WORK_DIR/register.json" <<'PY'
import json, os, sys
print(json.dumps({
    "displayName": sys.argv[1],
    "email": sys.argv[2],
    "password": os.environ["SMARTTASK_DEMO_PASSWORD"],
}))
PY
  auth_post /api/v1/auth/register "$WORK_DIR/register.json" > "$WORK_DIR/auth_body.json" || true
  AUTH_HTTP_CODE="$(cat "$WORK_DIR/auth_http_code")"
  body="$(cat "$WORK_DIR/auth_body.json")"
  if [[ "$AUTH_HTTP_CODE" == "201" ]]; then
    log "Registered $display_name <$email>"
    REGISTERED=$((REGISTERED + 1))
  elif [[ "$AUTH_HTTP_CODE" == "400" ]] || [[ "$AUTH_HTTP_CODE" == "409" ]]; then
    log "User already exists, skipping register: $email"
    SKIPPED_EXISTING=$((SKIPPED_EXISTING + 1))
  elif [[ "$AUTH_HTTP_CODE" == "429" ]]; then
    fail "Auth rate limited while registering $email — retry later or raise SMARTTASK_AUTH_DELAY_SEC"
  else
    fail "Register failed for $email (HTTP $AUTH_HTTP_CODE): $body"
  fi
  auth_pause
done <<< "$DEMO_USERS_SPEC"

# --- Admin login --------------------------------------------------------------
refresh_csrf
python3 - "$ADMIN_EMAIL" > "$WORK_DIR/login.json" <<'PY'
import json, os, sys
print(json.dumps({
    "email": sys.argv[1],
    "password": os.environ["SMARTTASK_ADMIN_PASSWORD"],
}))
PY
auth_post /api/v1/auth/login "$WORK_DIR/login.json" > "$WORK_DIR/auth_body.json"
AUTH_HTTP_CODE="$(cat "$WORK_DIR/auth_http_code")"
login_body="$(cat "$WORK_DIR/auth_body.json")"
if [[ "$AUTH_HTTP_CODE" != "200" ]]; then
  fail "Admin login failed (HTTP $AUTH_HTTP_CODE): $login_body"
fi
ACCESS_TOKEN="$(printf '%s' "$login_body" | json_get "['accessToken']")"
ADMIN_USER_ID="$(printf '%s' "$login_body" | json_get "['userId']")"
log "Logged in as admin ($ADMIN_EMAIL)"

# --- Resolve users + promote roles --------------------------------------------
USERS_JSON="$(api GET '/api/v1/admin/users?pageNumber=1&pageSize=100&sortBy=email&sortDescending=false')"

python3 - "$USERS_JSON" "$DEMO_USERS_SPEC" > "$STATE" <<'PY'
import json, sys

users = json.loads(sys.argv[1])["items"]
by_email = {u["email"].lower(): u for u in users}

desired = []
for line in sys.argv[2].strip().splitlines():
    name, email, role = line.split("|", 2)
    desired.append({"displayName": name, "email": email, "role": role})

missing = [d["email"] for d in desired if d["email"].lower() not in by_email]
if missing:
    raise SystemExit(f"Missing registered users: {', '.join(missing)}")

# Keep demo@ if present as Team Member target.
demo = by_email.get("demo@smarttask.local")
resolved = []
for d in desired:
    u = by_email[d["email"].lower()]
    resolved.append({
        "userId": u["userId"],
        "displayName": u["displayName"],
        "email": u["email"],
        "desiredRole": d["role"],
        "currentRoles": u["roles"],
        "isActive": u["isActive"],
    })

print(json.dumps({
    "usersByEmail": {k: {"userId": v["userId"], "roles": v["roles"], "displayName": v["displayName"], "isActive": v["isActive"]} for k, v in by_email.items()},
    "demoPersonas": resolved,
    "demoUserId": demo["userId"] if demo else None,
}, indent=2))
PY

ROLE_UPDATES=0
python3 -c 'import json
d=json.load(open("'"$STATE"'"))
for u in d["demoPersonas"]:
    need = "no" if u["desiredRole"] in (u["currentRoles"] or []) else "yes"
    print("%s\t%s\t%s" % (u["userId"], u["desiredRole"], need))
' > "$WORK_DIR/role_updates.tsv"
while IFS="$(printf '\t')" read -r user_id role needs_update; do
  [[ -z "${user_id:-}" ]] && continue
  if [[ "$needs_update" != "yes" ]]; then
    log "Role already set for $user_id -> $role"
    continue
  fi
  api PUT "/api/v1/admin/users/$user_id/role" \
    --data "$(python3 -c 'import json,sys; print(json.dumps({"role": sys.argv[1]}))' "$role")" \
    >/dev/null
  log "Set role $role for $user_id"
  ROLE_UPDATES=$((ROLE_UPDATES + 1))
done < "$WORK_DIR/role_updates.tsv"

# Refresh user map after role changes
USERS_JSON="$(api GET '/api/v1/admin/users?pageNumber=1&pageSize=100&sortBy=email&sortDescending=false')"
python3 - "$USERS_JSON" "$STATE" > "$WORK_DIR/state2.json" <<'PY'
import json, sys
users = json.loads(sys.argv[1])["items"]
prev = json.load(open(sys.argv[2]))
prev["usersByEmail"] = {
    u["email"].lower(): {
        "userId": u["userId"],
        "roles": u["roles"],
        "displayName": u["displayName"],
        "isActive": u["isActive"],
    }
    for u in users
}
for p in prev["demoPersonas"]:
    u = prev["usersByEmail"][p["email"].lower()]
    p["currentRoles"] = u["roles"]
print(json.dumps(prev, indent=2))
PY
mv "$WORK_DIR/state2.json" "$STATE"

# Member pool: demo personas + demo@ if present
MEMBER_IDS="$(python3 -c 'import json; d=json.load(open("'"$STATE"'"));
ids=[u["userId"] for u in d["demoPersonas"]];
demo=d.get("demoUserId");
print(" ".join(ids + ([demo] if demo and demo not in ids else [])))')"

# --- Projects -----------------------------------------------------------------
# Create named projects if missing; activate / set status as needed.
PROJECT_SPEC=$(cat <<'EOF'
Customer Portal Redesign|Rebuild the self-service portal with accessibility and SSO.|Active
Mobile App v2.0|Native mobile experience for field technicians and customers.|Active
Data Warehouse Migration|Move analytics pipelines to the cloud warehouse.|Active
Q4 Marketing Campaign|Plan multi-channel launch assets and tracking.|Planning
Legacy CRM Sunset|Pause integration work pending vendor contract renewal.|OnHold
Office Relocation|Completed facilities move and IT cutover.|Completed
EOF
)

# Map status names to enum ints
status_int() {
  case "$1" in
    Planning) echo 1 ;;
    Active) echo 2 ;;
    Completed) echo 3 ;;
    OnHold) echo 4 ;;
    Archived) echo 5 ;;
    *) fail "Unknown project status: $1" ;;
  esac
}

EXISTING_PROJECTS="$(api GET '/api/v1/projects?pageNumber=1&pageSize=100&sortBy=createdAt&sortDescending=false')"
PROJECTS_CREATED=0
ACTIVE_PROJECT_IDS=()

while IFS='|' read -r pname pdesc pstatus; do
  [[ -z "${pname:-}" ]] && continue
  existing_id="$(python3 -c 'import json,sys
items=json.loads(sys.argv[1])["items"]
name=sys.argv[2]
match=next((p for p in items if p["name"]==name), None)
print(match["id"] if match else "")
' "$EXISTING_PROJECTS" "$pname")"

  if [[ -n "$existing_id" ]]; then
    project_json="$(api GET "/api/v1/projects/$existing_id")"
    log "Project exists: $pname ($existing_id)"
  else
    project_json="$(api POST /api/v1/projects --data "$(python3 -c 'import json,sys; print(json.dumps({"name":sys.argv[1],"description":sys.argv[2]}))' "$pname" "$pdesc")")"
    PROJECTS_CREATED=$((PROJECTS_CREATED + 1))
    log "Created project: $pname"
  fi

  pid="$(printf '%s' "$project_json" | json_get "['id']")"
  current_status="$(printf '%s' "$project_json" | json_get "['status']")"
  row_version="$(printf '%s' "$project_json" | json_get "['rowVersion']")"
  name_now="$(printf '%s' "$project_json" | json_get "['name']")"
  desc_now="$(printf '%s' "$project_json" | python3 -c 'import json,sys; d=json.load(sys.stdin); print(d.get("description") or "")')"
  want_status="$(status_int "$pstatus")"

  if [[ "$current_status" != "$want_status" ]]; then
    project_json="$(api PUT "/api/v1/projects/$pid" --data "$(python3 -c 'import json,sys; print(json.dumps({"name":sys.argv[1],"description":sys.argv[2] or None,"status":int(sys.argv[3]),"rowVersion":sys.argv[4]}))' "$name_now" "$desc_now" "$want_status" "$row_version")")"
    log "Updated status for $pname -> $pstatus"
  fi

  if [[ "$pstatus" == "Active" ]]; then
    ACTIVE_PROJECT_IDS+=("$pid")
  fi

  # Add all demo members (ignore already-member errors by checking first)
  for mid in $MEMBER_IDS; do
    members_json="$(api GET "/api/v1/projects/$pid/members" || echo '[]')"
    already="$(python3 -c 'import json,sys
members=json.loads(sys.argv[1])
uid=sys.argv[2]
print("yes" if any(m.get("userId")==uid for m in members) else "no")
' "$members_json" "$mid")"
    if [[ "$already" == "yes" ]]; then
      continue
    fi
    if ! api POST "/api/v1/projects/$pid/members" \
      --data "$(python3 -c 'import json,sys; print(json.dumps({"userId":sys.argv[1]}))' "$mid")" \
      >/dev/null; then
      log "WARN: could not add member $mid to $pname"
    fi
  done
done <<< "$PROJECT_SPEC"

# Refresh active projects from API in case re-run (bash 3.2-compatible; no mapfile).
EXISTING_PROJECTS="$(api GET '/api/v1/projects?pageNumber=1&pageSize=100&sortBy=createdAt&sortDescending=false')"
# Only seed tasks onto the three Active demo projects we manage (members are
# guaranteed). Older e2e projects like Website Relaunch are left alone.
ACTIVE_PROJECT_IDS=()
while IFS= read -r pid; do
  [[ -z "${pid:-}" ]] && continue
  ACTIVE_PROJECT_IDS+=("$pid")
done <<EOF
$(python3 -c 'import json,sys
for p in json.loads(sys.argv[1])["items"]:
    if p["status"]==2 and p["name"] in {
        "Customer Portal Redesign","Mobile App v2.0","Data Warehouse Migration"
    }:
        print(p["id"])
' "$EXISTING_PROJECTS")
EOF

if [[ ${#ACTIVE_PROJECT_IDS[@]} -lt 1 ]]; then
  fail "No active projects available for task seeding"
fi

# --- Tasks --------------------------------------------------------------------
# Build assignee rotation from member pool.
TASKS_CREATED=0
TASK_STATUSES_SET=0

python3 - "$STATE" > "$WORK_DIR/assignees.txt" <<'PY'
import json
d=json.load(open(__import__("sys").argv[1]))
ids=[u["userId"] for u in d["demoPersonas"]]
demo=d.get("demoUserId")
if demo and demo not in ids:
    ids.append(demo)
print("\n".join(ids))
PY
ASSIGNEES=()
while IFS= read -r aid; do
  [[ -z "${aid:-}" ]] && continue
  ASSIGNEES+=("$aid")
done < "$WORK_DIR/assignees.txt"
ASSIGNEE_COUNT=${#ASSIGNEES[@]}

# 22 tasks: title|description|priority(1-4)|status(1-4)|due_offset_days|project_index|assignee_index
TASK_SPEC=$(cat <<'EOF'
Draft information architecture|Map portal IA and navigation.|3|2|-3|0|0
Implement SSO callback|Wire OpenID connect callback handler.|4|2|-1|0|1
Accessibility audit fixes|Resolve WCAG AA findings from audit.|3|1|0|0|2
Design system tokens|Publish color/type tokens for portal.|2|3|5|0|3
Customer FAQ content|Write help-center articles for launch.|1|1|10|0|4
Portal empty states|Ship empty-state illustrations and copy.|2|1|14|0|5|0
Checkout edge-case tests|Cover failed payment and retry paths.|3|2|-5|0|1
Push notification opt-in|Add mobile push consent flow.|2|1|2|1|2
Offline sync queue|Queue mutations while offline.|4|2|-2|1|3
Field tech job list|List assigned jobs with filters.|3|1|0|1|4
Biometric login|Enable FaceID/TouchID unlock.|2|3|7|1|0
Crash analytics dashboards|Wire crash reporting to ops dashboards.|3|4|12|1|1
App store screenshots|Prepare store listing creatives.|1|1|20|1|2
Deep link routing|Handle marketing deep links in app.|2|2|4|1|3
Warehouse schema finalization|Freeze star-schema for migration.|4|2|-4|2|4
ETL cutover rehearsal|Dry-run weekend cutover plan.|3|2|-1|2|5|0
Historical data backfill|Backfill 24 months of facts.|3|1|0|2|0
BI dashboard parity checks|Compare legacy vs new KPI totals.|2|1|3|2|1
Row-level security policies|Apply RLS for regional analysts.|4|1|9|2|2
Deprecate legacy extracts|Retire unused on-prem extract jobs.|2|3|15|2|3
Cost anomaly alerts|Alert on warehouse spend spikes.|3|1|21|2|4
Documentation handoff|Ops runbook for warehouse support.|1|4|30|2|5|0
EOF
)

# Fix accidental extra fields in a few lines - normalize to 7 fields
# (I may have typed |0 extra on some lines). Clean with python.
python3 - "$TASK_SPEC" > "$WORK_DIR/tasks.tsv" <<'PY'
import sys
raw = sys.argv[1]
for line in raw.strip().splitlines():
    parts = line.split("|")
    # title|desc|priority|status|due|proj|assignee  (take first 7)
    if len(parts) < 7:
        raise SystemExit(f"bad task line: {line}")
    print("|".join(parts[:7]))
PY

assignee_at() {
  local idx="$1"
  echo "${ASSIGNEES[$((idx % ASSIGNEE_COUNT))]}"
}

project_at() {
  local idx="$1"
  local n=${#ACTIVE_PROJECT_IDS[@]}
  echo "${ACTIVE_PROJECT_IDS[$((idx % n))]}"
}

iso_due() {
  local offset_days="$1"
  python3 -c 'from datetime import datetime, timedelta, timezone
import sys
days=int(sys.argv[1])
dt=datetime.now(timezone.utc).replace(hour=17, minute=0, second=0, microsecond=0)+timedelta(days=days)
print(dt.strftime("%Y-%m-%dT%H:%M:%SZ"))
' "$offset_days"
}

while IFS='|' read -r title desc priority status due_off proj_idx asg_idx; do
  [[ -z "${title:-}" ]] && continue
  pid="$(project_at "$proj_idx")"
  assignee="$(assignee_at "$asg_idx")"
  due="$(iso_due "$due_off")"

  # Skip creating duplicate titles in the same project
  existing_tasks="$(api GET "/api/v1/projects/$pid/tasks?pageNumber=1&pageSize=100&search=$(python3 -c 'import urllib.parse,sys; print(urllib.parse.quote(sys.argv[1]))' "$title")")"
  dup="$(python3 -c 'import json,sys
items=json.loads(sys.argv[1])["items"]
title=sys.argv[2]
print("yes" if any(t["title"]==title for t in items) else "no")
' "$existing_tasks" "$title")"
  if [[ "$dup" == "yes" ]]; then
    log "Task exists, skipping: $title"
    continue
  fi

  task_json="$(api POST "/api/v1/projects/$pid/tasks" --data "$(python3 -c 'import json,sys; print(json.dumps({
    "title": sys.argv[1],
    "description": sys.argv[2],
    "priority": int(sys.argv[3]),
    "assignedToUserId": sys.argv[4],
    "dueDateUtc": sys.argv[5],
  }))' "$title" "$desc" "$priority" "$assignee" "$due")")"
  TASKS_CREATED=$((TASKS_CREATED + 1))
  tid="$(printf '%s' "$task_json" | json_get "['id']")"
  row="$(printf '%s' "$task_json" | json_get "['rowVersion']")"
  cur_status="$(printf '%s' "$task_json" | json_get "['status']")"

  if [[ "$cur_status" != "$status" ]]; then
    api PUT "/api/v1/tasks/$tid/status" --data "$(python3 -c 'import json,sys; print(json.dumps({"status": int(sys.argv[1]), "rowVersion": sys.argv[2]}))' "$status" "$row")" >/dev/null
    TASK_STATUSES_SET=$((TASK_STATUSES_SET + 1))
  fi
  log "Created task: $title (assignee=$assignee)"
done < "$WORK_DIR/tasks.tsv"

# --- Verification -------------------------------------------------------------
DASH="$(api GET /api/v1/admin/dashboard)"
USERS_JSON="$(api GET '/api/v1/admin/users?pageNumber=1&pageSize=100&isActive=true')"
PROJECTS_JSON="$(api GET '/api/v1/projects?pageNumber=1&pageSize=100')"

python3 - "$DASH" "$USERS_JSON" "$PROJECTS_JSON" "$STATE" <<'PY'
import json, sys

dash = json.loads(sys.argv[1])
users = json.loads(sys.argv[2])["items"]
projects = json.loads(sys.argv[3])["items"]
state = json.load(open(sys.argv[4]))

active_users = [u for u in users if u.get("isActive")]
roles = set()
for u in active_users:
    roles.update(u.get("roles") or [])

print("=== Seed summary ===")
print(f"Active users: {len(active_users)}")
print(f"Roles present: {', '.join(sorted(roles))}")
print(f"Projects: {len(projects)} (dashboard total={dash['projects']['totalProjects']})")
print(f"  Planning={dash['projects']['planningProjects']} Active={dash['projects']['activeProjects']} OnHold={dash['projects']['onHoldProjects']} Completed={dash['projects']['completedProjects']}")
print(f"Tasks: {dash['totalTasks']} (ToDo={dash['toDoTasks']} InProgress={dash['inProgressTasks']} Completed={dash['completedTasks']} Cancelled={dash['cancelledTasks']} Overdue={dash['overdueTasks']})")
print()
print("Demo accounts (shared password from SMARTTASK_DEMO_PASSWORD):")
print(f"{'Display Name':<22} {'Email':<32} Role")
print("-" * 78)
# Always show admin separately (different password)
print(f"{'Development Admin':<22} {'e2e-admin@smarttask.local':<32} Admin")
if any(u["email"].lower()=="demo@smarttask.local" for u in users):
    print(f"{'Demo User':<22} {'demo@smarttask.local':<32} Team Member")
for p in state["demoPersonas"]:
    role = (p["currentRoles"] or ["?"])[0]
    print(f"{p['displayName']:<22} {p['email']:<32} {role}")

ok = (
    len(active_users) >= 5
    and "Admin" in roles
    and "Project Manager" in roles
    and "Team Member" in roles
    and len(projects) >= 5
    and dash["totalTasks"] >= 20
)
print()
if not ok:
    print("VERIFICATION FAILED: minimums not met", file=sys.stderr)
    sys.exit(1)
print("VERIFICATION PASSED")
PY

# Count assigned tasks across active seeded projects
ASSIGNED=0
for pid in "${ACTIVE_PROJECT_IDS[@]}"; do
  tjson="$(api GET "/api/v1/projects/$pid/tasks?pageNumber=1&pageSize=100")"
  c="$(python3 -c 'import json,sys; items=json.loads(sys.argv[1])["items"]; print(sum(1 for t in items if t.get("assignedToUserId")))' "$tjson")"
  ASSIGNED=$((ASSIGNED + c))
done
log "Assigned tasks across seeded active projects: $ASSIGNED"
log "Created this run: users+=$REGISTERED (skipped_existing=$SKIPPED_EXISTING) roles_updated=$ROLE_UPDATES projects+=$PROJECTS_CREATED tasks+=$TASKS_CREATED status_updates=$TASK_STATUSES_SET"
