# Claude Code Hooks

Hooks fire on Claude Code events to automate checks and enforce constraints.

## Configuration

Register hooks in `.claude/settings.local.json` — not `settings.json`. Local settings are gitignored, keeping your personal hook configuration out of source control.

## Requirements

- `jq` must be installed (`brew install jq`)
- Must be run from within a git repository

## Testing & Debugging

- **Verbose mode**: Press `Ctrl+O` in Claude Code to see hook execution details
- **Debug mode**: Run `claude --debug` for full execution logging

## Disabling

- Use the `/hooks` menu in Claude Code to toggle individual hooks
- Or set `"disableAllHooks": true` in `.claude/settings.local.json`

---

## seeder-docs-check.sh

**Event:** Stop

**Purpose:** Reminds developers to update Seeder documentation when code in
`util/Seeder/`, `util/SeederApi/`, or `util/SeederUtility/` was modified but no
`.md` files in those directories were touched.

**How it works:**

1. Runs `git diff` to detect all changed files
2. If non-markdown files were changed under a Seeder project AND no `.md` files
   in any of the three Seeder projects were modified, blocks the stop with a
   reminder (intentionally cross-project — a doc update anywhere in the Seeder
   subsystem satisfies the check)
3. The reminder lists all `.md` files in the affected projects (discovered dynamically)
4. On the next stop, `stop_hook_active` is true, so the hook allows through
5. Result: one reminder per stop, then the developer decides

---

## rust-sdk-surface-check.sh

**Event:** Stop

**Purpose:** Ensures the RustSdk API surface reference stays current when the
sdk-internal dependency rev is bumped. Prevents `.claude/skills/bump-rust-sdk/references/api-surface.md`
from going stale.

**How it works:**

1. Runs `git diff` to detect all changed files
2. If `util/RustSdk/rust/Cargo.toml` was modified BUT
   `.claude/skills/bump-rust-sdk/references/api-surface.md` was NOT, blocks the
   stop with a reminder to regenerate the API surface inventory
3. On the next stop, `stop_hook_active` is true, so the hook allows through
4. Result: one reminder per stop — Claude reads the `.rs` source files and
   regenerates the reference

---

## block-mutating-sql.sh

**Event:** PreToolUse (fires before the Bash tool executes)

**Registration:** Skill frontmatter in `.claude/skills/querying-bitwarden-database/SKILL.md` — checked in and active for anyone using the skill.

**Purpose:** Enforces read-only access to the Bitwarden database when Claude runs `sqlcmd` commands. Denies mutating operations before they reach the database.

**How it works:**

1. Triggers on Bash commands containing `sqlcmd`
2. **Layer 1** — denies mutating SQL keywords: `INSERT`, `UPDATE`, `DELETE`, `DROP`, `ALTER`, `TRUNCATE`, `CREATE`, `MERGE`, `EXEC`, `BULK INSERT`, `SELECT INTO`, `GRANT`, `REVOKE`, `DENY`
3. **Layer 2** — denies dangerous primitives: `xp_*`, `sp_executesql`, `sp_OA*`, `RECONFIGURE`, `OPENROWSET`/`OPENQUERY`/`OPENDATASOURCE`
4. **Layer 3** — denies sqlcmd escape commands: `:!!` (OS shell) and `:r` (file inclusion)
5. Returns `permissionDecision: deny` with a descriptive message on any match; allows through otherwise
