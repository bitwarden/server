# <ticket or PR>: Seeder regression findings

Run <YYYY-MM-DD>, <start>–<end> UTC. Branch `<branch>` at `<short sha>`, database `<database>`, vault at `<vault url>`. Plan: `<plan file>`. Screenshots: `<folder>`.

**<One sentence: the defects found, or "No defects found.">**

## Summary

| Area | Preset(s) | Result |
| --- | --- | --- |
| Preflight | n/a | <Pass or Fail. Test pass count; services up> |
| <step name> | `<preset>` | <Pass or Fail, in one line> |

## <step name>

<One section per row in the summary table. Counts against the baseline or verification.md, as a table when several presets share columns. Quote error lines exactly.>

## SQL proved vs UI showed

- SQL: <what the queries proved>
- UI: <what the browser showed, and which accounts nobody has looked at>

## Not run, and why

- <step name>: <reason>

## Deviations from the plan

- <what changed and why>

## Follow-ups

- <item>

## Seeds

Every account uses the Seeder's default master password (`util/Seeder/CLAUDE.md`) unless `--password` was passed.

| Preset | Org | Org ID | Owner |
| --- | --- | --- | --- |
| `<preset>` | <mangled org name> | `<org id>` | `<owner email>` |

Seeded orgs stay in `<database>`.
