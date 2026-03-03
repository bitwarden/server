#!/bin/bash
# rust-sdk-surface-check.sh
# Stop hook: reminds developers to update the RustSdk API surface reference
# when Cargo.toml rev pins were modified but api-surface.md was not touched.
#
# Behavior: blocks Claude from stopping exactly once with a reminder.
# On the second stop (stop_hook_active=true), allows through.

set -euo pipefail

INPUT=$(cat)

# Guard: if a Stop hook already blocked this turn, allow through.
STOP_HOOK_ACTIVE=$(echo "$INPUT" | jq -r '.stop_hook_active // false')
if [[ "$STOP_HOOK_ACTIVE" == "true" ]]; then
  exit 0
fi

CWD=$(echo "$INPUT" | jq -r '.cwd')

# Gather all changed files (staged, unstaged, and untracked) relative to repo root.
DIFF_HEAD=$(git -C "$CWD" diff --name-only HEAD 2>/dev/null || true)
UNTRACKED=$(git -C "$CWD" ls-files --others --exclude-standard 2>/dev/null || true)
ALL_CHANGED=$(printf "%s\n%s" "$DIFF_HEAD" "$UNTRACKED" | sort -u | grep -v '^$' || true)

if [[ -z "$ALL_CHANGED" ]]; then
  exit 0
fi

# Check if the RustSdk Cargo.toml was modified.
if ! echo "$ALL_CHANGED" | grep -q '^util/RustSdk/rust/Cargo.toml$'; then
  exit 0
fi

# Check if the API surface reference was already updated.
if echo "$ALL_CHANGED" | grep -q '^\.claude/skills/bump-rust-sdk/references/api-surface.md$'; then
  exit 0
fi

REASON="util/RustSdk/rust/Cargo.toml was modified but the API surface reference was not updated. Read all .rs files in util/RustSdk/rust/src/ and regenerate .claude/skills/bump-rust-sdk/references/api-surface.md to reflect the current imports, types, and traits used from bitwarden-core, bitwarden-crypto, and bitwarden-vault."

jq -n --arg reason "$REASON" '{ "decision": "block", "reason": $reason }'
