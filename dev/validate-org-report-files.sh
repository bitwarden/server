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
#   ./validate-org-report-files.sh                # validate ALL unvalidated file rows
#   ./validate-org-report-files.sh <organizationId>   # scope to one org
#
# Requires: the bitwardenserver-mssql-1 container running, and dev/.env with MSSQL_PASSWORD.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DEV_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

CONTAINER="bitwardenserver-mssql-1"
DB="vault_dev"
SQLCMD="/opt/mssql-tools18/bin/sqlcmd"

ORG_FILTER=""
if [[ "${1:-}" != "" ]]; then
  ORG_FILTER="AND OrganizationId = '${1}'"
fi

PW="$(grep -E '^MSSQL_PASSWORD=' "$DEV_DIR/.env" | cut -d= -f2-)"
if [[ -z "$PW" ]]; then
  echo "Could not read MSSQL_PASSWORD from $DEV_DIR/.env" >&2
  exit 1
fi

docker exec "$CONTAINER" "$SQLCMD" -S localhost -U SA -P "$PW" -C -d "$DB" -h -1 -W -s "|" -Q "
SET NOCOUNT ON;
UPDATE OrganizationReport
SET ReportFile = JSON_MODIFY(ReportFile, '\$.Validated', CAST(1 AS BIT)),
    RevisionDate = SYSUTCDATETIME()
WHERE ReportFile IS NOT NULL
  AND ISJSON(ReportFile) = 1
  AND ISNULL(JSON_VALUE(ReportFile, '\$.Validated'), 'false') <> 'true'
  ${ORG_FILTER};
SELECT CONCAT('rows validated this run: ', @@ROWCOUNT);
SELECT Id,
  CONVERT(varchar(30), CreationDate, 126) AS Created,
  JSON_VALUE(ReportFile, '\$.Validated') AS Validated,
  JSON_VALUE(ReportFile, '\$.Size') AS Size
FROM OrganizationReport
WHERE ReportFile IS NOT NULL AND ISJSON(ReportFile) = 1 ${ORG_FILTER}
ORDER BY CreationDate DESC;"
