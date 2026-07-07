# Recorded Baseline — 2026-07-07

Fixture: `scale.md-balanced-sterling-cooper --mangle` on local MSSQL; ground truth captured via `capture-ground-truth.sh`. One run per configuration (variance not yet measured — treat single-case flips as directional). Timing/token axes unavailable. Raw run transcripts were working artifacts and are regenerable by re-running the cases in `evals.json` against a fresh fixture.

## Configurations

- **no-skill baseline**: no skill; given only env-var + sqlcmd hints, so the comparison isolates domain knowledge, not connection discovery.
- **v1**: the 605-prose-line skill as of the Opus-era expansion.
- **v2**: the trimmed 259-line skill (this version) after ablation.

## Results (assertions passed / total)

| Eval                                | no-skill | v1       | v2       |
| ----------------------------------- | -------- | -------- | -------- |
| 0 active-member-count               | 0/2      | 2/2      | 2/2      |
| 1 member-status-breakdown           | 2/2      | 2/2      | 2/2      |
| 2 user-visible-ciphers              | 3/3      | 3/3      | 3/3      |
| 3 top-users-direct-collection       | 3/3      | 3/3      | 3/3      |
| 4 active-vs-deleted-ciphers         | 2/2      | 2/2      | 2/2      |
| 5 archived-org-ciphers              | 0/2      | 2/2      | 2/2      |
| 6 personal-ciphers-of-members       | 1/2      | 2/2      | 2/2      |
| 7 org-plan-and-active-state         | 1/2      | 2/2      | 2/2      |
| 8 encrypted-column-trap             | 2/2      | 2/2      | 2/2      |
| 9 org-abilities-via-view            | 2/2      | 2/2      | 2/2      |
| 13 collection-permission-resolution | 2/2      | 2/2      | 2/2      |
| **Mean per-eval pass rate**         | **0.71** | **1.00** | **1.00** |

## What the skill flips (the three cases no-skill failed)

- **eval-0**: "active member" — no-skill used the occupied-seat reading (`Status IN (0,1,2)` → 239); canonical is Confirmed (`Status = 2` → 214).
- **eval-5**: archive state — no-skill queried the plausible-but-never-written `ArchivedDate` column (→ 0) and rationalized it; truth is the `Archives` per-user JSON (→ 50), which is what `Cipher_Archive` writes.
- **eval-7**: active flag — no-skill co-cited `Organization.Status`; canonical is `Enabled`.

## Ablation conclusion

Everything else tied: no-skill found the correct joins, the `UserCipherDetails`/`UserCollectionDetails` functions, `DeletedDate` semantics, enum values (more completely than the skill's drifted table — it knew `Staged = 3`), and avoided the encrypted-column LIKE trap by reading `src/Sql` and enum sources. Those surfaces were cut (the deletions and their per-case evidence are this PR's diff and this table). The bar for re-adding any instruction: a case in `evals.json` that fails without it.

## Deferred (parked, with price of admission)

| Capability                                                                   | Build when                                                                                        |
| ---------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------- |
| Write-mode seeding/mutation support ("this shouldn't be just about reading") | A safe write workflow is designed with its own eval set and permission model                      |
| MySQL/PostgreSQL provider references + per-provider read-only hooks          | The multi-provider seed-and-verify loop produces validated per-provider runbooks                  |
| Send-liveness and AuthRequest-expiry grounding rules                         | Both tables are empty database-wide today; a preset seeds them and a case exercises the semantics |

Eval-fixture improvements for the next iteration (grader-flagged): give eval-13 a direct-RO=1/group-RO=0 conflict so the count alone discriminates precedence; give eval-9 an org with mixed `Use*` flags so "all enabled" can't pass trivially.

## Provider validation

Sterling Cooper seeded per provider by flipping `globalSettings:databaseProvider`; identical counts on MSSQL/Dapper, PostgreSQL, and MySQL (status mix 12/13/12/214; ciphers 5,000/25 deleted/50 archived; 50 groups; 500 collections; all payloads `2.`-prefixed EncStrings). Read-only guards in the provider references were verified by rejected write attempts on all three providers.

## Triggering (description optimization, 5 iterations)

Precision 100% (0 false fires across all near-miss negatives, including production asks and write requests), recall 6–17% regardless of description wording — organic auto-triggering is weak for structural reasons (sandboxed trials lacked DB env vars; Claude Code under-consults skills it can substitute with raw Bash). The skill is intended to be reliable via explicit invocation (`user-invocable: true`). Trigger cases: [trigger-eval-set.json](trigger-eval-set.json).
