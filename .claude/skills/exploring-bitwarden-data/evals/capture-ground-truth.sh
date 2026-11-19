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
# user43 reaches some items through more than one path, so COUNT(*) and COUNT(DISTINCT Id) differ
PROBE_USER_EMAIL="${MANGLE_PREFIX}+user43@sterlingcooper.example"
PERM_PROBE_USER_EMAIL="${MANGLE_PREFIX}+user90@sterlingcooper.example"

# Hand-curated grading text, kept verbatim so a rerun preserves it
PERSONAL_GRADING_NOTE="Prompt says 'own in total' — ambiguous. Either value passes when the run states its interpretation. Ambiguity found during iteration-1; rubric applied identically to both configs."
PERM_NOTE="Most-permissive-wins coincides with correct here; grader must inspect SQL for direct-precedence (COALESCE(CU.x, CG.x, 0), or MIN(ReadOnly) per collection over UserCollectionDetails) to award full marks."

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

read -r PERSONAL_TOTAL PERSONAL_ACTIVE <<< "$(q "
SELECT COUNT(*), SUM(CASE WHEN C.DeletedDate IS NULL THEN 1 ELSE 0 END) FROM dbo.Cipher C
WHERE C.OrganizationId IS NULL AND C.UserId IN (
  SELECT OU.UserId FROM dbo.OrganizationUser OU
  WHERE OU.OrganizationId = '$ORG_ID' AND OU.UserId IS NOT NULL)")"

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
# UserCipherDetails returns one row per access path; count items
read -r PROBE_ROWS PROBE_VISIBLE PROBE_ACTIVE <<< "$(q "
SELECT COUNT(*), COUNT(DISTINCT Id), COUNT(DISTINCT CASE WHEN DeletedDate IS NULL THEN Id END)
FROM dbo.UserCipherDetails('$PROBE_USER_ID')")"

PERM_USER_ID=$(q "SELECT CAST(Id AS VARCHAR(36)) FROM dbo.[User] WHERE Email = '$PERM_PROBE_USER_EMAIL'")
PERM_OU_ID=$(q "SELECT CAST(Id AS VARCHAR(36)) FROM dbo.OrganizationUser WHERE UserId = '$PERM_USER_ID' AND OrganizationId = '$ORG_ID'")

# Server resolution (Collection_ReadByUserId): a direct grant beats groups, MIN merges groups
read -r PERM_ACCESSIBLE PERM_EDITABLE <<< "$(q "
SELECT COUNT(*), SUM(CASE WHEN RO = 0 THEN 1 ELSE 0 END)
FROM (SELECT Id, MIN([ReadOnly]) RO FROM dbo.UserCollectionDetails('$PERM_USER_ID')
      WHERE OrganizationId = '$ORG_ID' GROUP BY Id) X")"

# The wrong answer eval 13 catches: every path counts and the most restrictive wins
PERM_EDITABLE_RESTRICTIVE=$(q "
SELECT SUM(CASE WHEN RO = 0 THEN 1 ELSE 0 END) FROM (
  SELECT CollectionId, MAX(RO) RO FROM (
    SELECT CollectionId, CAST([ReadOnly] AS INT) RO FROM dbo.CollectionUser WHERE OrganizationUserId = '$PERM_OU_ID'
    UNION ALL
    SELECT CG.CollectionId, CAST(CG.[ReadOnly] AS INT) FROM dbo.GroupUser GU
    JOIN dbo.CollectionGroup CG ON CG.GroupId = GU.GroupId WHERE GU.OrganizationUserId = '$PERM_OU_ID'
  ) P GROUP BY CollectionId) Y")

CONFLICTS=$(q "
SELECT CAST(CU.CollectionId AS VARCHAR(36)), CAST(CU.[ReadOnly] AS INT), CAST(CG.[ReadOnly] AS INT)
FROM dbo.CollectionUser CU
JOIN dbo.GroupUser GU ON GU.OrganizationUserId = CU.OrganizationUserId
JOIN dbo.CollectionGroup CG ON CG.GroupId = GU.GroupId AND CG.CollectionId = CU.CollectionId
WHERE CU.OrganizationUserId = '$PERM_OU_ID' AND CU.[ReadOnly] <> CG.[ReadOnly]")
[ "$(echo "$CONFLICTS" | grep -c .)" -eq 1 ] || { echo "Expected exactly one direct-vs-group conflict for user90; re-curate eval 13." >&2; exit 1; }
read -r CONFLICT_COLLECTION CONFLICT_DIRECT CONFLICT_GROUP <<< "$CONFLICTS"

SENDS=$(q "SELECT COUNT(*) FROM dbo.Send")
AUTH_REQUESTS=$(q "SELECT COUNT(*) FROM dbo.AuthRequest")

# Both are relative to now, so recapture before grading evals 11 and 12
LIVE_SENDS=$(q "
SELECT COUNT(*) FROM dbo.Send
WHERE DeletionDate > SYSUTCDATETIME()
  AND (ExpirationDate IS NULL OR ExpirationDate > SYSUTCDATETIME())
  AND Disabled = 0
  AND (MaxAccessCount IS NULL OR AccessCount < MaxAccessCount)")

# 15 minutes for Type 0/1, 7 days for Type 2 (AdminApproval) - GlobalSettings.PasswordlessAuth
PENDING_AUTH_REQUESTS=$(q "
SELECT COUNT(*) FROM dbo.AuthRequest
WHERE ResponseDate IS NULL
  AND ((Type IN (0, 1) AND CreationDate > DATEADD(MINUTE, -15, SYSUTCDATETIME()))
    OR (Type = 2 AND CreationDate > DATEADD(DAY, -7, SYSUTCDATETIME())))")

TOP_DIRECT_JSON=$(echo "$TOP_DIRECT" | jq -R 'split(":") | {email: .[0], collections: (.[1] | tonumber)}' | jq -s .)

OUT_DIR="$(cd "$(dirname "$0")" && pwd)"
jq -a -n \
  --arg captured "$(date +%Y-%m-%d)" \
  --arg org_id "$ORG_ID" --arg org_name "$ORG_NAME" \
  --arg probe_email "$PROBE_USER_EMAIL" --arg probe_id "$PROBE_USER_ID" \
  --arg perm_email "$PERM_PROBE_USER_EMAIL" --arg perm_id "$PERM_USER_ID" \
  --argjson revoked "$REVOKED" --argjson invited "$INVITED" \
  --argjson accepted "$ACCEPTED" --argjson confirmed "$CONFIRMED" --argjson total "$TOTAL" \
  --argjson ciphers_total "$CIPHERS_TOTAL" --argjson ciphers_deleted "$CIPHERS_DELETED" \
  --argjson ciphers_archived "$CIPHERS_ARCHIVED" \
  --argjson personal_total "$PERSONAL_TOTAL" --argjson personal_active "$PERSONAL_ACTIVE" \
  --arg grading_note "$PERSONAL_GRADING_NOTE" \
  --argjson plan_type "$PLAN_TYPE" --argjson enabled "$ENABLED" \
  --argjson use_groups "$USE_GROUPS" --argjson use_sso "$USE_SSO" \
  --argjson use_api "$USE_API" --argjson use_events "$USE_EVENTS" \
  --argjson top_direct "$TOP_DIRECT_JSON" \
  --argjson probe_rows "$PROBE_ROWS" --argjson probe_visible "$PROBE_VISIBLE" \
  --argjson probe_active "$PROBE_ACTIVE" \
  --argjson sends "$SENDS" --argjson auth_requests "$AUTH_REQUESTS" \
  --argjson live_sends "$LIVE_SENDS" --argjson pending_ar "$PENDING_AUTH_REQUESTS" \
  --argjson perm_accessible "$PERM_ACCESSIBLE" --argjson perm_editable "$PERM_EDITABLE" \
  --argjson perm_editable_restrictive "$PERM_EDITABLE_RESTRICTIVE" \
  --arg conflict_collection "$CONFLICT_COLLECTION" \
  --argjson conflict_direct "$CONFLICT_DIRECT" --argjson conflict_group "$CONFLICT_GROUP" \
  --arg perm_note "$PERM_NOTE" \
  '{
    captured: $captured,
    fixture: { preset: "scale.md-balanced-sterling-cooper" },
    slugs: { ORG_NAME: $org_name, ORG_ID: $org_id, PROBE_USER_EMAIL: $probe_email, PERM_PROBE_USER_EMAIL: $perm_email },
    values: {
      members_by_status: { "revoked_-1": $revoked, invited_0: $invited, accepted_1: $accepted, confirmed_2: $confirmed, total: $total },
      active_member_count: $confirmed,
      org_ciphers_total: $ciphers_total,
      org_ciphers_deleted: $ciphers_deleted,
      org_ciphers_active: ($ciphers_total - $ciphers_deleted),
      org_ciphers_archived: $ciphers_archived,
      personal_ciphers_of_members: { total_including_trashed: $personal_total, active_only: $personal_active, grading_note: $grading_note },
      org_plan: { plan_type: $plan_type, enabled: $enabled, use_groups: $use_groups, use_sso: $use_sso, use_api: $use_api, use_events: $use_events },
      top_direct_collection_access: $top_direct,
      probe_user: { email: $probe_email, id: $probe_id, visible_ciphers_via_UserCipherDetails: $probe_visible, rows_before_distinct: $probe_rows, visible_active_only: $probe_active },
      sends_total_db: $sends,
      live_sends: $live_sends,
      auth_requests_total_db: $auth_requests,
      pending_auth_requests: $pending_ar,
      members_by_status_names: { "-1": "Revoked", "0": "Invited", "1": "Accepted", "2": "Confirmed" },
      permission_probe_user90: {
        email: $perm_email,
        id: $perm_id,
        total_accessible_collections: $perm_accessible,
        editable_correct_direct_precedence: $perm_editable,
        editable_if_most_restrictive_wins: $perm_editable_restrictive,
        conflict: { collection: $conflict_collection, direct_ReadOnly: $conflict_direct, group_ReadOnly: $conflict_group },
        note: $perm_note
      }
    }
  }' > "$OUT_DIR/ground-truth.json"

echo "Wrote $OUT_DIR/ground-truth.json for org '$ORG_NAME' ($ORG_ID)"
