# Claude Code Hooks

All hooks are Stop hooks — they fire when Claude finishes responding and check
whether documentation or references need updating based on what was changed.

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
