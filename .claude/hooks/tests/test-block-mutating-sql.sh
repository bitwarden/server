#!/bin/bash
# Test harness for .claude/hooks/block-mutating-sql.sh
# Prints a checklist of BLOCKED / ALLOWED results and exits non-zero on any failure.

HOOK="$(cd "$(dirname "$0")/.." && pwd)/block-mutating-sql.sh"

if [[ ! -f "$HOOK" ]]; then
  echo "ERROR: Hook not found at $HOOK"
  exit 1
fi

PASS=0
FAIL=0

# Build a JSON payload the Claude Code harness would send
payload() { jq -n --arg cmd "$1" '{"tool_input":{"command":$cmd}}'; }

# Returns "deny" if the hook blocks, "allow" if it passes through
decision() {
  local out
  out=$(payload "$1" | bash "$HOOK" 2>/dev/null)
  echo "$out" | jq -r '.hookSpecificOutput.permissionDecision // "allow"' 2>/dev/null || echo "allow"
}

assert_deny() {
  local label="$1" cmd="$2"
  local result
  result=$(decision "$cmd")
  if [[ "$result" == "deny" ]]; then
    echo "  ✅ BLOCKED  $label"
    ((PASS++))
  else
    echo "  ❌ FAIL     $label  (expected BLOCKED, got ALLOWED)"
    ((FAIL++))
  fi
}

assert_allow() {
  local label="$1" cmd="$2"
  local result
  result=$(decision "$cmd")
  if [[ "$result" != "deny" ]]; then
    echo "  ✅ ALLOWED  $label"
    ((PASS++))
  else
    echo "  ❌ FAIL     $label  (expected ALLOWED, got BLOCKED)"
    ((FAIL++))
  fi
}

# Gap tests — document known behavior, never fail the suite
note_gap() {
  local label="$1" cmd="$2" expected="$3"
  local result
  result=$(decision "$cmd")
  if [[ "$result" == "deny" ]]; then
    echo "  ℹ️  GAP-CLOSED  $label  (now BLOCKED)"
  else
    echo "  ⚠️  GAP        $label  (ALLOWED — $expected)"
  fi
}

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  block-mutating-sql.sh — test checklist"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

# ── Trigger bypass ────────────────────────────────────────
echo ""
echo "[ Trigger bypass — hook should not fire ]"

assert_allow "T1  which sqlcmd (no BW_DB/SQLCMDPASSWORD)" \
  "which sqlcmd"

assert_allow "T2  sqlcmd --version (no BW_DB/SQLCMDPASSWORD)" \
  "sqlcmd --version"

assert_allow "T3  echo BW_DB (no sqlcmd in command)" \
  "echo BW_DB"

# ── Legitimate reads ──────────────────────────────────────
echo ""
echo "[ Legitimate reads — hook fires but should pass through ]"

assert_allow "R1  Plain SELECT" \
  'SQLCMDPASSWORD="pw" sqlcmd -S localhost -Q "SELECT Id FROM [dbo].[Cipher]"'

assert_allow "R2  WITH CTE" \
  'SQLCMDPASSWORD="pw" sqlcmd -S localhost -Q "WITH c AS (SELECT Id FROM [dbo].[Cipher]) SELECT * FROM c"'

assert_allow "R3  INFORMATION_SCHEMA query" \
  'SQLCMDPASSWORD="pw" sqlcmd -S localhost -Q "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES"'

assert_allow "R4  sys.* query" \
  'SQLCMDPASSWORD="pw" sqlcmd -S localhost -Q "SELECT name FROM sys.tables"'

assert_allow "R5  Column names containing keywords (DeletedDate, CreatedAt)" \
  'SQLCMDPASSWORD="pw" sqlcmd -S localhost -Q "SELECT DeletedDate, CreatedDate FROM [dbo].[Cipher]"'

assert_allow "R6  Email address containing \"grant@\" (false-positive trap)" \
  'SQLCMDPASSWORD="pw" sqlcmd -S localhost -Q "SELECT Email FROM [dbo].[User] WHERE Email = '"'"'grant@example.com'"'"'"'

# ── Layer 1: SQL keyword blocks ───────────────────────────
echo ""
echo "[ Layer 1 — SQL keyword blocks ]"

assert_deny "L1-01  INSERT (uppercase)" \
  'SQLCMDPASSWORD="pw" sqlcmd -S localhost -Q "INSERT INTO [dbo].[Cipher] VALUES (1)"'

assert_deny "L1-02  insert into (lowercase)" \
  'SQLCMDPASSWORD="pw" sqlcmd -S localhost -Q "insert into [dbo].[Cipher] VALUES (1)"'

assert_deny "L1-03  Insert Into (mixed case)" \
  'SQLCMDPASSWORD="pw" sqlcmd -S localhost -Q "Insert Into [dbo].[Cipher] VALUES (1)"'

assert_deny "L1-04  UPDATE" \
  'SQLCMDPASSWORD="pw" sqlcmd -S localhost -Q "UPDATE [dbo].[User] SET Email = '"'"'x@x.com'"'"'"'

assert_deny "L1-05  DELETE" \
  'SQLCMDPASSWORD="pw" sqlcmd -S localhost -Q "DELETE FROM [dbo].[Cipher] WHERE Id = 1"'

assert_deny "L1-06  DROP" \
  'SQLCMDPASSWORD="pw" sqlcmd -S localhost -Q "DROP TABLE [dbo].[Cipher]"'

assert_deny "L1-07  ALTER" \
  'SQLCMDPASSWORD="pw" sqlcmd -S localhost -Q "ALTER TABLE [dbo].[Cipher] ADD Col INT"'

assert_deny "L1-08  TRUNCATE" \
  'SQLCMDPASSWORD="pw" sqlcmd -S localhost -Q "TRUNCATE TABLE [dbo].[Event]"'

assert_deny "L1-09  CREATE" \
  'SQLCMDPASSWORD="pw" sqlcmd -S localhost -Q "CREATE TABLE Tmp (Id INT)"'

assert_deny "L1-10  MERGE" \
  'SQLCMDPASSWORD="pw" sqlcmd -S localhost -Q "MERGE [dbo].[Cipher] AS target USING src ON 1=1"'

assert_deny "L1-11  EXEC with trailing space" \
  'SQLCMDPASSWORD="pw" sqlcmd -S localhost -Q "EXEC sp_something"'

assert_deny "L1-12  EXECUTE with trailing space" \
  'SQLCMDPASSWORD="pw" sqlcmd -S localhost -Q "EXECUTE sp_something"'

assert_deny "L1-13  BULK INSERT" \
  'SQLCMDPASSWORD="pw" sqlcmd -S localhost -Q "BULK INSERT [dbo].[Cipher] FROM '"'"'C:\data.csv'"'"'"'

assert_deny "L1-14  SELECT INTO (new table creation)" \
  'SQLCMDPASSWORD="pw" sqlcmd -S localhost -Q "SELECT * INTO [dbo].[CipherCopy] FROM [dbo].[Cipher]"'

assert_deny "L1-15  GRANT" \
  'SQLCMDPASSWORD="pw" sqlcmd -S localhost -Q "GRANT SELECT ON [dbo].[Cipher] TO [SomeUser]"'

assert_deny "L1-16  REVOKE" \
  'SQLCMDPASSWORD="pw" sqlcmd -S localhost -Q "REVOKE SELECT ON [dbo].[Cipher] FROM [SomeUser]"'

assert_deny "L1-17  DENY" \
  'SQLCMDPASSWORD="pw" sqlcmd -S localhost -Q "DENY SELECT ON [dbo].[Cipher] TO [SomeUser]"'

assert_deny "L1-18  DELETE via bare -P flag (no SQLCMDPASSWORD token)" \
  'sqlcmd -S localhost -U sa -P hunter2 -d vault -Q "DELETE FROM [dbo].[Cipher] WHERE Id = 1"'

# ── Layer 2: Dangerous procs ──────────────────────────────
echo ""
echo "[ Layer 2 — Dangerous proc blocks ]"

assert_deny "L2-01  xp_cmdshell" \
  'SQLCMDPASSWORD="pw" sqlcmd -S localhost -Q "EXEC xp_cmdshell '"'"'whoami'"'"'"'

assert_deny "L2-02  xp_fileexist" \
  'SQLCMDPASSWORD="pw" sqlcmd -S localhost -Q "EXEC xp_fileexist '"'"'C:\\temp'"'"'"'

assert_deny "L2-03  sp_executesql" \
  'SQLCMDPASSWORD="pw" sqlcmd -S localhost -Q "EXEC sp_executesql N'"'"'SELECT 1'"'"'"'

assert_deny "L2-04  sp_OACreate" \
  'SQLCMDPASSWORD="pw" sqlcmd -S localhost -Q "EXEC sp_OACreate '"'"'ADODB.Connection'"'"', @conn OUT"'

assert_deny "L2-05  RECONFIGURE" \
  'SQLCMDPASSWORD="pw" sqlcmd -S localhost -Q "RECONFIGURE"'

assert_deny "L2-06  OPENROWSET (no space before paren)" \
  'SQLCMDPASSWORD="pw" sqlcmd -S localhost -Q "SELECT * FROM OPENROWSET('"'"'SQLNCLI'"'"', '"'"'Server=.'"'"', '"'"'SELECT 1'"'"')"'

assert_deny "L2-07  OPENROWSET (space before paren)" \
  'SQLCMDPASSWORD="pw" sqlcmd -S localhost -Q "SELECT * FROM OPENROWSET ('"'"'SQLNCLI'"'"', '"'"'Server=.'"'"', '"'"'SELECT 1'"'"')"'

assert_deny "L2-08  OPENQUERY" \
  'SQLCMDPASSWORD="pw" sqlcmd -S localhost -Q "SELECT * FROM OPENQUERY(LinkedServer, '"'"'SELECT 1'"'"')"'

assert_deny "L2-09  OPENDATASOURCE" \
  'SQLCMDPASSWORD="pw" sqlcmd -S localhost -Q "SELECT * FROM OPENDATASOURCE('"'"'SQLNCLI'"'"', '"'"'Data Source=.'"'"')"'

# ── Layer 3: sqlcmd escape blocks ─────────────────────────
echo ""
echo "[ Layer 3 — sqlcmd escape blocks ]"

assert_deny "L3-01  :!! shell escape in heredoc" \
  'SQLCMDPASSWORD="pw" sqlcmd -S localhost <<'"'"'SQL'"'"'
:!! whoami
SQL'

assert_deny "L3-02  :r file include" \
  'SQLCMDPASSWORD="pw" sqlcmd -S localhost <<'"'"'SQL'"'"'
:r C:\evil.sql
SQL'

# ── Gap exposure ──────────────────────────────────────────
echo ""
echo "[ Gap exposure — documents known behavior, never fails the suite ]"

assert_deny "G1  EXEC( without trailing space (dynamic SQL)" \
  'SQLCMDPASSWORD="pw" sqlcmd -S localhost -Q "EXEC('"'"'SELECT 1'"'"')"'

assert_deny "G2  EXECUTE( without trailing space" \
  'SQLCMDPASSWORD="pw" sqlcmd -S localhost -Q "EXECUTE('"'"'SELECT 1'"'"')"'

note_gap "G3  sp_OAStop (not in the Layer 2 sp_OA regex)" \
  'SQLCMDPASSWORD="pw" sqlcmd -S localhost -Q "EXEC sp_OAStop @conn"' \
  "closed by Layer 1 — EXEC[[:space:](] catches 'EXEC ' before Layer 2 runs"

note_gap "G4  xp_ with no letter after underscore (XP_[A-Z] requires one letter)" \
  'SQLCMDPASSWORD="pw" sqlcmd -S localhost -Q "EXEC xp_"' \
  "closed by Layer 1 — EXEC[[:space:](] catches 'EXEC ' before the xp_ check runs"

# ── Summary ───────────────────────────────────────────────
echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  Results: $PASS passed, $FAIL failed"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

[[ $FAIL -eq 0 ]]
