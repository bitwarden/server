#!/bin/bash
# seeder-docs-check.sh
# Stop hook: reminds developers to update Seeder documentation when
# Seeder code was modified but no documentation files were touched.
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

# Check which Seeder projects have non-markdown code changes.
SEEDER_CODE_CHANGED=false
SEEDER_PROJECTS_CHANGED=()

for project in "util/Seeder/" "util/SeederApi/" "util/SeederUtility/"; do
  if echo "$ALL_CHANGED" | grep -q "^${project}" && \
     echo "$ALL_CHANGED" | grep "^${project}" | grep -qv '\.md$'; then
    SEEDER_CODE_CHANGED=true
    SEEDER_PROJECTS_CHANGED+=("$project")
  fi
done

if [[ "$SEEDER_CODE_CHANGED" == "false" ]]; then
  exit 0
fi

# Check if any Seeder .md files were already modified.
if echo "$ALL_CHANGED" | grep -qE '^util/(Seeder|SeederApi|SeederUtility)/.*\.md$'; then
  exit 0
fi

# Dynamically discover all .md files in each modified project.
DOCS_LIST=""
for project in "${SEEDER_PROJECTS_CHANGED[@]}"; do
  while IFS= read -r md_file; do
    DOCS_LIST="${DOCS_LIST}\n  - ${md_file}"
  done < <(find "$CWD/$project" -name "*.md" | sed "s|^$CWD/||" | sort)
done

REASON=$(printf "Seeder code was modified but no Seeder documentation was updated. Please check whether any of these docs need updating:%b\n\nIf the docs are already accurate, let the user know you verified them." "$DOCS_LIST")

jq -n --arg reason "$REASON" '{ "decision": "block", "reason": $reason }'
