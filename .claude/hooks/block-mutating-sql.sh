#!/bin/bash
set -euo pipefail

# PreToolUse hook: enforces read-only access to the Bitwarden SQL Server
# database when invoked via the Go-based `sqlcmd` CLI.
#
# Trigger: fires on any Bash command that contains `sqlcmd`, except for
#   safe non-query uses — path lookups (`which sqlcmd`, `type sqlcmd`,
#   `command -v sqlcmd`) and info flags (`sqlcmd --version`, `sqlcmd -?`) —
#   that cannot execute SQL. Unrelated commands pass through on the first check.
#
# Three check layers:
#   1. Mutating SQL keywords (INSERT, UPDATE, DELETE, DROP, ALTER, CREATE,
#      TRUNCATE, MERGE, EXEC, BULK INSERT, SELECT INTO, GRANT, REVOKE, DENY)
#   2. Stored procedures, external data access, and privilege escalation
#      (xp_*, sp_executesql, sp_OA*, RECONFIGURE, OPENROWSET/OPENQUERY/
#      OPENDATASOURCE)
#   3. sqlcmd interactive escape commands (`:!!` runs OS commands; `:r` reads
#      arbitrary files into the script — both bypass the read-only contract)

input=$(cat)
command=$(echo "$input" | jq -r '.tool_input.command // empty' 2>/dev/null) || {
  printf '{"hookSpecificOutput":{"permissionDecision":"deny"},"systemMessage":"Hook: failed to parse payload — denying as a safety measure."}\n'
  exit 0
}

# Pass through anything that isn't a sqlcmd invocation
if [[ ! "$command" =~ sqlcmd ]]; then
  exit 0
fi

# Allow safe non-query uses that cannot execute SQL
if [[ "$command" =~ (which|type|command[[:space:]]+-v)[[:space:]]+sqlcmd ]] || \
   [[ "$command" =~ sqlcmd[[:space:]]+(--version|-\?) ]]; then
  exit 0
fi

# Normalize to uppercase and collapse newlines for case-insensitive, multi-line matching
upper_command=$(echo "$command" | tr '[:lower:]' '[:upper:]' | tr '\n\t\r' '   ')

# --- Layer 1: Mutating SQL keywords ---
# Each keyword requires trailing whitespace to avoid false positives on
# table/column names (e.g. "DeletedDate", "CreatedAt").
sql_deny_patterns=(
  'INSERT[[:space:]]'
  'UPDATE[[:space:]]'
  'DELETE[[:space:]]'
  'DROP[[:space:]]'
  'ALTER[[:space:]]'
  'TRUNCATE[[:space:]]'
  'CREATE[[:space:]]'
  'MERGE[[:space:]]'
  'EXEC[[:space:](]'
  'EXECUTE[[:space:](]'
  'BULK[[:space:]]+INSERT'
  # `[[:space:]]INTO[[:space:]]` catches `SELECT <cols> INTO <table>` (creates
  # a new table). `INSERT INTO` is caught earlier by the `INSERT ` rule, so
  # this generic INTO check only fires on the SELECT-INTO case. The trade-off
  # is a possible false-positive on string literals containing " INTO " — for
  # this defensive read-only hook, that's acceptable.
  '[[:space:]]INTO[[:space:]]'
  'GRANT[[:space:]]'
  'REVOKE[[:space:]]'
  'DENY[[:space:]]'
)

for pattern in "${sql_deny_patterns[@]}"; do
  if echo "$upper_command" | grep -qE "$pattern"; then
    cat <<HOOKEOF
{
  "hookSpecificOutput": {
    "permissionDecision": "deny"
  },
  "systemMessage": "BLOCKED by Bitwarden read-only hook: Mutating SQL detected (pattern: $pattern). The Skill(exploring-bitwarden-data) is strictly read-only. Only SELECT, WITH (CTE), and INFORMATION_SCHEMA/sys queries are permitted."
}
HOOKEOF
    exit 0
  fi
done

# --- Layer 2: Stored procedures, external data access, privilege escalation ---
# xp_           = extended stored procedures (e.g. xp_cmdshell — OS execution)
# sp_executesql = arbitrary dynamic SQL execution
# sp_OA*        = OLE Automation procs (create COM objects, execute code)
# RECONFIGURE   = applies sp_configure changes (e.g. enabling xp_cmdshell)
# OPENROWSET / OPENQUERY / OPENDATASOURCE = external data source access
if echo "$upper_command" | grep -qE 'XP_[A-Z]'; then
  cat <<HOOKEOF
{
  "hookSpecificOutput": {
    "permissionDecision": "deny"
  },
  "systemMessage": "BLOCKED by Bitwarden read-only hook: Extended stored procedure detected (xp_ prefix). These can execute OS commands and are never permitted by the read-only skill."
}
HOOKEOF
  exit 0
fi

if echo "$upper_command" | grep -qE 'SP_EXECUTESQL'; then
  cat <<HOOKEOF
{
  "hookSpecificOutput": {
    "permissionDecision": "deny"
  },
  "systemMessage": "BLOCKED by Bitwarden read-only hook: sp_executesql detected. Dynamic SQL execution is not permitted by the read-only skill."
}
HOOKEOF
  exit 0
fi

if echo "$upper_command" | grep -qE 'SP_OA(CREATE|METHOD|GETPROPERTY|SETPROPERTY|DESTROY)'; then
  cat <<HOOKEOF
{
  "hookSpecificOutput": {
    "permissionDecision": "deny"
  },
  "systemMessage": "BLOCKED by Bitwarden read-only hook: OLE Automation stored procedure detected (sp_OA*). These can create COM objects and execute arbitrary code."
}
HOOKEOF
  exit 0
fi

if echo "$upper_command" | grep -qE 'RECONFIGURE'; then
  cat <<HOOKEOF
{
  "hookSpecificOutput": {
    "permissionDecision": "deny"
  },
  "systemMessage": "BLOCKED by Bitwarden read-only hook: RECONFIGURE detected. Server configuration changes are not permitted by the read-only skill."
}
HOOKEOF
  exit 0
fi

if echo "$upper_command" | grep -qE '(OPENROWSET|OPENQUERY|OPENDATASOURCE)[[:space:]]*\('; then
  cat <<HOOKEOF
{
  "hookSpecificOutput": {
    "permissionDecision": "deny"
  },
  "systemMessage": "BLOCKED by Bitwarden read-only hook: External data access function detected (OPENROWSET/OPENQUERY/OPENDATASOURCE). The read-only skill does not permit external data source access."
}
HOOKEOF
  exit 0
fi

# --- Layer 3: sqlcmd interactive escape commands ---
# `:!!` runs OS commands from inside sqlcmd scripts (a heredoc-fed query can
# contain `:!!whoami` and break out of the read-only contract). `:r <path>`
# reads files into the script, which we also disallow defensively.
# Match leading whitespace + literal colon + bang-bang or r-space.
if echo "$command" | grep -qE '(^|[[:space:]:])\:\!\!'; then
  cat <<HOOKEOF
{
  "hookSpecificOutput": {
    "permissionDecision": "deny"
  },
  "systemMessage": "BLOCKED by Bitwarden read-only hook: sqlcmd shell-escape \`:!!\` detected. OS command execution from inside sqlcmd scripts is never permitted by the read-only skill."
}
HOOKEOF
  exit 0
fi

if echo "$command" | grep -qE '(^|[[:space:]])\:r[[:space:]]'; then
  cat <<HOOKEOF
{
  "hookSpecificOutput": {
    "permissionDecision": "deny"
  },
  "systemMessage": "BLOCKED by Bitwarden read-only hook: sqlcmd file-include \`:r\` detected. Including arbitrary files is not permitted; pass SQL via -Q or via heredoc to /dev/stdin."
}
HOOKEOF
  exit 0
fi

exit 0
