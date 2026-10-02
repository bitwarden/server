#!/bin/bash
set -euo pipefail

# Regenerates ground-truth.json for the eval set after (re-)seeding the
# scale.md-balanced-sterling-cooper preset. Read-only: SELECTs only.
#
# Usage: capture-ground-truth.sh [org-id]
#   org-id  optional; defaults to the most recently created org whose
#           name ends in 'Sterling Cooper' (mangle prefixes vary per seed).
#
# Requires BW_MSSQL_SERVER / BW_MSSQL_DB_NAME / BW_MSSQL_USERNAME / BW_MSSQL_PASSWORD.

for v in BW_MSSQL_SERVER BW_MSSQL_DB_NAME BW_MSSQL_USERNAME BW_MSSQL_PASSWORD; do
  [ -n "${!v:-}" ] || { echo "Missing env var: $v" >&2; exit 1; }
done

q() {
  SQLCMDPASSWORD="$BW_MSSQL_PASSWORD" sqlcmd \
    -S "$BW_MSSQL_SERVER" -U "$BW_MSSQL_USERNAME" -d "$BW_MSSQL_DB_NAME" \
    -C -N m -K ReadOnly -h -1 -W \
    -Q "SET NOCOUNT ON; $1" | tr -d '\r' | sed '/^$/d'
}

ORG_ID=${1:-$(q "SELECT TOP 1 CAST(Id AS VARCHAR(36)) FROM dbo.Organization WHERE [Name] LIKE '%Sterling Cooper' ORDER BY CreationDate DESC")}
[ -n "$ORG_ID" ] || { echo "No Sterling Cooper org found — seed the preset first." >&2; exit 1; }

ORG_NAME=$(q "SELECT [Name] FROM dbo.Organization WHERE Id = '$ORG_ID'")
MANGLE_PREFIX=${ORG_NAME%-Sterling Cooper}
PROBE_USER_EMAIL="${MANGLE_PREFIX}+user5@sterlingcooper.example"

read -r REVOKED INVITED ACCEPTED CONFIRMED TOTAL <<< "$(q "
SELECT
  SUM(CASE WHEN [Status] = -1 THEN 1 ELSE 0 END),
  SUM(CASE WHEN [Status] = 0 THEN 1 ELSE 0 END),
  SUM(CASE WHEN [Status] = 1 THEN 1 ELSE 0 END),
  SUM(CASE WHEN [Status] = 2 THEN 1 ELSE 0 END),
  COUNT(*)
FROM dbo.OrganizationUser WHERE OrganizationId = '$ORG_ID'")"

read -r CIPHERS_TOTAL CIPHERS_DELETED CIPHERS_ARCHIVED <<< "$(q "
SELECT COUNT(*),
  SUM(CASE WHEN DeletedDate IS NOT NULL THEN 1 ELSE 0 END),
  SUM(CASE WHEN Archives IS NOT NULL THEN 1 ELSE 0 END)
FROM dbo.Cipher WHERE OrganizationId = '$ORG_ID'")"

PERSONAL_CIPHERS=$(q "
SELECT COUNT(*) FROM dbo.Cipher C
WHERE C.OrganizationId IS NULL AND C.UserId IN (
  SELECT OU.UserId FROM dbo.OrganizationUser OU
  WHERE OU.OrganizationId = '$ORG_ID' AND OU.UserId IS NOT NULL)")

read -r PLAN_TYPE ENABLED USE_GROUPS USE_SSO USE_API USE_EVENTS <<< "$(q "
SELECT PlanType, CAST(Enabled AS INT), CAST(UseGroups AS INT), CAST(UseSso AS INT), CAST(UseApi AS INT), CAST(UseEvents AS INT)
FROM dbo.Organization WHERE Id = '$ORG_ID'")"

TOP_DIRECT=$(q "
SELECT TOP 5 U.Email + ':' + CAST(COUNT(DISTINCT CU.CollectionId) AS VARCHAR)
FROM dbo.CollectionUser CU
JOIN dbo.OrganizationUser OU ON CU.OrganizationUserId = OU.Id
JOIN dbo.[User] U ON OU.UserId = U.Id
WHERE OU.OrganizationId = '$ORG_ID'
GROUP BY U.Email ORDER BY COUNT(DISTINCT CU.CollectionId) DESC")

PROBE_USER_ID=$(q "SELECT CAST(Id AS VARCHAR(36)) FROM dbo.[User] WHERE Email = '$PROBE_USER_EMAIL'")
PROBE_VISIBLE=$(q "SELECT COUNT(*) FROM dbo.UserCipherDetails('$PROBE_USER_ID')")

SENDS=$(q "SELECT COUNT(*) FROM dbo.Send")
AUTH_REQUESTS=$(q "SELECT COUNT(*) FROM dbo.AuthRequest")

TOP_DIRECT_JSON=$(echo "$TOP_DIRECT" | jq -R 'split(":") | {email: .[0], collections: (.[1] | tonumber)}' | jq -s .)

OUT_DIR="$(cd "$(dirname "$0")" && pwd)"
jq -n \
  --arg captured "$(date +%Y-%m-%d)" \
  --arg org_id "$ORG_ID" --arg org_name "$ORG_NAME" \
  --arg probe_email "$PROBE_USER_EMAIL" --arg probe_id "$PROBE_USER_ID" \
  --argjson revoked "$REVOKED" --argjson invited "$INVITED" \
  --argjson accepted "$ACCEPTED" --argjson confirmed "$CONFIRMED" --argjson total "$TOTAL" \
  --argjson ciphers_total "$CIPHERS_TOTAL" --argjson ciphers_deleted "$CIPHERS_DELETED" \
  --argjson ciphers_archived "$CIPHERS_ARCHIVED" --argjson personal "$PERSONAL_CIPHERS" \
  --argjson plan_type "$PLAN_TYPE" --argjson enabled "$ENABLED" \
  --argjson use_groups "$USE_GROUPS" --argjson use_sso "$USE_SSO" \
  --argjson use_api "$USE_API" --argjson use_events "$USE_EVENTS" \
  --argjson top_direct "$TOP_DIRECT_JSON" \
  --argjson probe_visible "$PROBE_VISIBLE" \
  --argjson sends "$SENDS" --argjson auth_requests "$AUTH_REQUESTS" \
  '{
    captured: $captured,
    fixture: { preset: "scale.md-balanced-sterling-cooper" },
    slugs: { ORG_NAME: $org_name, ORG_ID: $org_id, PROBE_USER_EMAIL: $probe_email },
    values: {
      members_by_status: { "revoked_-1": $revoked, invited_0: $invited, accepted_1: $accepted, confirmed_2: $confirmed, total: $total },
      active_member_count: $confirmed,
      org_ciphers_total: $ciphers_total,
      org_ciphers_deleted: $ciphers_deleted,
      org_ciphers_active: ($ciphers_total - $ciphers_deleted),
      org_ciphers_archived: $ciphers_archived,
      personal_ciphers_of_members: $personal,
      org_plan: { plan_type: $plan_type, enabled: $enabled, use_groups: $use_groups, use_sso: $use_sso, use_api: $use_api, use_events: $use_events },
      top_direct_collection_access: $top_direct,
      probe_user: { email: $probe_email, id: $probe_id, visible_ciphers_via_UserCipherDetails: $probe_visible },
      sends_total_db: $sends,
      auth_requests_total_db: $auth_requests
    }
  }' > "$OUT_DIR/ground-truth.json"

echo "Wrote $OUT_DIR/ground-truth.json for org '$ORG_NAME' ($ORG_ID)"
