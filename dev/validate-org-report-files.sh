#!/usr/bin/env bash
#
# validate-org-report-files.sh
#
# Local-dev helper for Access Intelligence report file storage.
#
# When the file-storage flag is on, the client uploads the report payload as a
# blob (Azurite locally) and the server marks OrganizationReport.ReportFile.Validated
# only after an Azure Event Grid "BlobCreated" webhook fires. Azurite does not emit
# Event Grid events, so locally that flag never flips and the flag-on /latest read
# (which filters on Validated = 'true' OR ReportData <> '') skips file rows and
# returns a stale inline report instead.
#
# This script flips Validated = true on file-backed rows directly in the DB so the
# flag-on load path returns the genuine latest (file) report. The read query is not
# cached, so the change takes effect on the next /latest call.
#
# Usage:
#   ./validate-org-report-files.sh                    # validate ALL unvalidated file rows
#   ./validate-org-report-files.sh <organizationId>   # scope to one org
#
# Requires: dev/.env with MSSQL_PASSWORD, and the MSSQL container running.
#
# Configuration (override via environment variables or dev/.env):
#   BW_MSSQL_CONTAINER  Docker container name (default: bitwardenserver-mssql-1)
#   BW_MSSQL_DB         Database name         (default: vault_dev)
#   BW_SQLCMD           Path to sqlcmd        (default: /opt/mssql-tools18/bin/sqlcmd)

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DEV_DIR="$SCRIPT_DIR"

_env_val() { grep -E "^${1}=" "$DEV_DIR/.env" 2>/dev/null | cut -d= -f2- | tail -1; }
_resolve()  { local v; v="$(_env_val "$1")"; echo "${!1:-${v:-$2}}"; }

CONTAINER="$(_resolve BW_MSSQL_CONTAINER bitwardenserver-mssql-1)"
DB="$(_resolve BW_MSSQL_DB vault_dev)"
SQLCMD="$(_resolve BW_SQLCMD /opt/mssql-tools18/bin/sqlcmd)"

if [[ "${1:-}" != "" ]]; then
  if [[ ! "${1}" =~ ^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$ ]]; then
    echo "Error: organizationId must be a valid UUID" >&2
    exit 1
  fi
  ORG_FILTER="AND OrganizationId = '${1}'"
else
  ORG_FILTER=""
fi

PW="$(_resolve MSSQL_PASSWORD "")"
if [[ -z "$PW" ]]; then
  echo "Could not read MSSQL_PASSWORD from $DEV_DIR/.env" >&2
  exit 1
fi

_sqlcmd() { docker exec "$CONTAINER" "$SQLCMD" -S localhost -U SA -P "$PW" -C -d "$DB" "$@"; }

_sqlcmd -h -1 -W -Q "
SET NOCOUNT ON;
UPDATE OrganizationReport
SET ReportFile = JSON_MODIFY(ReportFile, '\$.Validated', CAST(1 AS BIT)),
    RevisionDate = SYSUTCDATETIME()
WHERE ReportFile IS NOT NULL
  AND ISJSON(ReportFile) = 1
  AND ISNULL(JSON_VALUE(ReportFile, '\$.Validated'), 'false') <> 'true'
  ${ORG_FILTER};
SELECT CONCAT('rows validated this run: ', @@ROWCOUNT);"

_sqlcmd -W -Q "
SET NOCOUNT ON;
SELECT Id,
  CONVERT(varchar(30), CreationDate, 126) AS Created,
  JSON_VALUE(ReportFile, '\$.Validated') AS Validated,
  JSON_VALUE(ReportFile, '\$.Size') AS Size
FROM OrganizationReport
WHERE ReportFile IS NOT NULL AND ISJSON(ReportFile) = 1 ${ORG_FILTER}
ORDER BY CreationDate DESC;"
