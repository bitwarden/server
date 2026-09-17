# Boundary review fixtures

Synthetic unified diffs used by `evals.json` to exercise the classifier in `../../SKILL.md`. Each targets a real file path so CODEOWNERS resolution and domain bucketing are genuine, and each names real columns on real tables.

The enclosing methods are invented. These diffs are not expected to apply to `HEAD` and are not compiled. What is measured is whether a reviewer correctly classifies the accesses on the added lines, resolves the owning code on both sides, and ranks writes above reads. Every added line uses only members that actually exist on the entity involved, so the classification question is well posed.

Ownership facts the assertions depend on, verified at commit `6798eb683`:

- `Organization`'s schema file is `src/Sql/dbo/Tables/Organization.sql`, in the unclassified root, so CODEOWNERS resolves it to `@bitwarden/dept-dbops` and the entity fallback (`src/Core/AdminConsole/Entities/Organization.cs`) gives admin-console.
- `Cipher`'s schema file is `src/Sql/dbo/Vault/Tables/Cipher.sql`. `**/Vault` appears after `src/Sql/**` in CODEOWNERS, and GitHub applies the last matching rule, so `src/Sql/dbo/Vault/Tables/Cipher.sql` resolves to team-vault-dev rather than dbops.
- `**/Tools` and `**/AdminConsole` also appear later in CODEOWNERS than `src/Sql/**`, so the same last-matching-rule reasoning gives team-tools-dev and team-admin-console-dev. Cite these rules by pattern and order, never by line number, which shifts on every CODEOWNERS insertion.
- `Organization.StorageBytesRemaining()` reads `MaxStorageGb` and `Storage` (`src/Core/AdminConsole/Entities/Organization.cs:435-458`).

| Fixture | Exercises | Expected |
| --- | --- | --- |
| 01 | Code in `src/Core/Vault` writes and reads `Organization` | 3 writes (S1) ranked above 1 read (S2) |
| 02 | Code in `src/Core/Vault` touches only `Cipher`, plus a `nameof` | zero crossings |
| 03 | Code in `src/Core/Tools` reads `Organization` via a helper and a property pattern | reads only, both columns behind the helper named |

Fixture 01 counts three writes: the two column assignments (`Storage`, `RevisionDate`) plus `_organizationRepository.ReplaceAsync(org)`, which maps to a full-row update and therefore writes every mapped column. `SKILL.md` Step 4 states that rule, and `evals.json` asserts all three. If you change one of those three files, change all three.
