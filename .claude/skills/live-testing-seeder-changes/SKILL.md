---
name: live-testing-seeder-changes
description: Perform a live regression-test of a Bitwarden Seeder change e2e on a local stack. Seeds the presets regression.md maps to the change, compares counts with a baseline through read-only SQL, runs a variant matrix for validator and shape changes, exercises SeederApi scenes and the Stripe canary, checks the web vault with the Playwright MCP, logs in through the local IdP when the change touches SSO, and writes a findings report. Use only when the user explicitly asks for a live or e2e test of a Seeder change. DO NOT use for ordinary seeding (use seeding-bitwarden-data) or for querying seeded data (use exploring-bitwarden-data).
when_to_use: Use for "regression testing the seeder", "e2e test this Seeder PR", "run the null/empty matrix Seeder regression", "verify a review fix on real seeds"
allowed-tools: Read, Skill, Write, Bash(dotnet test *), Bash(dotnet run --project util/SeederUtility:*)
argument-hint: "[sso]"
---

# Execute a live test of the Seeder

Work from the root of the server checkout under test (the main checkout or a `.worktrees/<name>` one). The Seeder docs hold the details; open the one each step names.

## Prerequisites

The web vault checks need the Playwright MCP. Before anything else, confirm a `browser_snapshot` tool is callable (search for it if tools are deferred).
If it is not, stop: tell the developer to install it (`claude plugin install playwright@claude-plugins-official`) and start a new session.

## Rules

- Read the database only through the `exploring-bitwarden-data` skill. Write to it only through the Seeder: the CLI (`preset`, `organization`, `individual`) and SeederApi create scenes. Never reset, clean or delete data by any other path, including the SeederApi `DELETE` endpoints, and never offer to; say what needs clearing and wait.
- Run the seeds, queries and browser checks yourself. Hand the developer a command only when the permission system blocks it.
- Pass `--mangle` on every seed, so runs stay additive and a preset can be re-seeded.
- The CLI prints the org ApiKey row on stdout. Redirect stdout and stderr to separate temp files, read stdout through the step 2 grep, which hides that row and masks any key-length token, and delete both files. Everything else it prints is mock data, including the master password, which is the documented default.
- Vault console and network output holds `access_token` URLs from the SignalR hub. Always pass `filename:` under `$RUN` to `browser_console_messages` and `browser_network_requests`, never print either inline, and read the file only through `grep -v 'access_token'`. `.playwright-mcp/*.log` holds the same output; never `Read` or `cat` it, and step 9 deletes it.
- Files under `dev/` (`.env`, `authsources.php`, `saml20-sp-remote.php`, `secrets.json`) are the developer's and gitignored. Never print values from them to the console. Show the edit; the developer applies it.
- When reading user-secrets or a connection string, parse the one key you need with `sed -nE`; on a miss print `<not found>`, never the value. Never run `dotnet user-secrets list` unfiltered.
- `BulkCommitter` has no transaction. Never kill a seed mid-commit.
- Give every `dotnet run`, `dotnet build` and `dotnet test` a 10-minute timeout (the first build compiles the Rust SDK). The 2-minute default would kill a long seed mid-commit.
- Copy GUIDs from tool output; never retype them.
- Keep every temp file in one folder, `RUN="${TMPDIR:-/tmp}/seeder-run"; mkdir -p "$RUN"; chmod 700 "$RUN"`. Set it at the top of each command, because shell state does not persist between calls. Step 9 deletes it with one `rm -rf`, so no glob can abort the cleanup. Run scripts and loops under `bash`: the session shell may be zsh, where an unmatched glob aborts the whole command and an unquoted variable is not split into words.

## Seeder documentation

Open each document when its row says; read `util/Seeder/CLAUDE.md` and regression.md before any seed.

| Source                                                     | Open it when                                                                                                      |
| ---------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------- |
| `util/Seeder/CLAUDE.md`                                    | At the start: pipeline, step order, the regression rule                                                           |
| `util/Seeder/Seeds/docs/regression.md`                     | Picking what to seed (changed path, preset, assertion), the Preflight services, Known non-regressions, SSO wiring |
| `util/Seeder/Seeds/docs/verification.md`                   | Writing SQL: Q1 to Q13 and the expected values per preset                                                         |
| `util/Seeder/Seeds/docs/access-shape.md`, `presets.md`     | The change touches `accessShape` or the preset catalog                                                            |
| `util/SeederUtility/README.md`, `util/SeederApi/README.md` | CLI flags; scenes and auth                                                                                        |

## Steps

### 1. Preflight

1. Record the branch, HEAD and `git status --porcelain`. The tree may hold only the change under test; that output is the baseline for steps 4.5 and 9.3.
2. Run `dotnet test test/SeederApi.IntegrationTest/` and record the pass count.
3. Check the services in regression.md's Preflight table. Browse at the host in `globalSettings:baseServiceUri:vault`; SSO from any other host fails with `Invalid redirect_uri`. Read it from user-secrets, not from `dev/`: `v=$(dotnet user-secrets list --id bitwarden-Api | sed -nE 's/^globalSettings:baseServiceUri:vault = (.*)$/\1/p'); [ -n "$v" ] && echo "$v" || echo '<not found>'`.
4. Run the read-only login check from `exploring-bitwarden-data`. If that login cannot open the self-host database, report rows written by SeederApi-SelfHost (`:5048`) as unverified.
5. SSO login is in scope when the skill arguments (`$ARGUMENTS`) include `sso`, when the developer asks for it, or when the change touches `SsoConfigSeeder`, `CreateSsoConfigStep` or `features.local-sso`. Otherwise skip step 8.
6. Write the plan: each changed path with its preset and assertion from regression.md, plus the extra checks the change needs. Ask where to save it, and put a timestamp in the file name so a rerun never overwrites an earlier plan.

### 2. Seed one preset at a time

```bash
RUN="${TMPDIR:-/tmp}/seeder-run"; mkdir -p "$RUN"; chmod 700 "$RUN"
dotnet run --project util/SeederUtility -- preset --name <name> --mangle > "$RUN/seed-<id>.out" 2> "$RUN/seed-<id>.err"; echo "exit=$?"
grep -vi 'apikey' "$RUN/seed-<id>.out" | sed -E 's/[A-Za-z0-9]{30,}/<redacted>/g'
grep -v $'\x1b' "$RUN/seed-<id>.err"
rm "$RUN/seed-<id>".{out,err}   # only after the mangle-map and SSO-wiring reads below
```

The same recipe and the `seed()` guard below apply to `organization` and `individual` runs when regression.md names one. Keep the two streams apart; the CLI separates them on purpose (`ConsoleOutput`, `Program.PrintBanner`). Stdout holds the result rows: IDs, owner, password, counts, the ApiKey and Stripe IDs. Stderr holds the ANSI banner, the `--- Mangled Data Map` block, the SSO wiring block and errors. Every banner line contains an ESC byte, so `grep -v $'\x1b'` on stderr drops the banner and keeps the rest; do not send stderr to `/dev/null`, because the mangle map and errors are there. The mangle-map header check reads the stderr file: `grep -q -- '--- Mangled Data Map' <file>.err`. There is no flag that turns the banner off. `--output json` exists only on `preset --list`: `preset --list --output json 2>/dev/null | jq -r '.organization[], .individual[]'` lists the preset names (the `build-seeded-databases.yml` workflow relies on the same split).

Parallel seeds make timings meaningless and a failure hard to pin on one preset. For a long series, write a sequential loop of step 2 recipes to `$RUN/seed.sh`, start `bash` on it in the background, and wait for its notification. Define this guard at the top of `seed.sh` and call `seed` instead of `dotnet run`; it refuses any seed without `--mangle`. Set `NOBUILD=1` to add `--no-build` (step 4.2). `preset --list` and `--help` write nothing and run directly.

```bash
seed() { case " $* " in *" --mangle "*) ;; *) echo "REFUSED: no --mangle: $*" >&2; return 2;; esac
         dotnet run --project util/SeederUtility ${NOBUILD:+--no-build} -- "$@"; }
```

Then match the running loop with `pgrep -f "[s]eed.sh"` (the brackets keep the pattern from matching the shell running `pgrep`), confirm from its output that the child seed finished, and to stop early kill only the loop shell.

### 3. Baseline and compare

1. For a change that should not move output, compare with a baseline: an earlier report, or the same presets seeded on the base commit.
2. Access-shape presets fix their shape with `accessShape.seed`, so counts must match exactly. Density presets derive the generator seed from the mangled domain, so check them against verification.md's ranges; regression.md lists that drift as a known non-regression.
3. Keep each query in a file under `$RUN` with an `__ORG__` placeholder and `sed` the org ID in, so every org gets the same SQL.
4. The seeder database can hold future-dated rows. Bound "created during this run" queries at both ends.

### 4. Variant matrix (validator, schema or shape changes)

1. Write one-change variants of a reference preset to an untracked `util/Seeder/Seeds/fixtures/presets/e2etmp/`: drop each block, empty each array, push each value past its range, break each cross-field total. Give each a unique `.example` domain and an org name of at most 41 characters (`Organization.Name` is 50; the mangle prefix takes 9).
2. Presets are embedded resources. Run `dotnet build util/SeederUtility` once, then seed each variant with the step 2 recipe, adding `--no-build` and using `--name e2etmp.<id>`.
3. Record the exit code and first error line, and classify each outcome: A sensible default, B clear error, C error that hides the cause, D silent wrong output.
4. For every failed variant, prove nothing committed: no org row with that name inside the run's time window.
5. Delete the folder, rebuild, and confirm `preset --list` shows no `e2etmp` entry and `git status --porcelain` matches the preflight baseline.

### 5. SeederApi scenes

1. `:5047` is cloud; `:5048` is SelfHost (`--launch-profile SeederApi-SelfHost`).
2. Read the SeederApi account from user-secrets into shell variables and pass them to `curl -u` without echoing them. Omit the `X-Play-Id` header the README examples send: with it, `DeleteOldPlayDataJob` deletes the scene's rows after a day; without it they persist like CLI seeds.

   ```bash
   u=$(dotnet user-secrets list --id bitwarden-seeder-api | sed -nE 's/^seederSettings:accounts:0:username = (.*)$/\1/p'); [ -n "$u" ] || echo '<not found>'
   p=$(dotnet user-secrets list --id bitwarden-seeder-api | sed -nE 's/^seederSettings:accounts:0:password = (.*)$/\1/p'); [ -n "$p" ] || echo '<not found>'
   curl -s -u "$u:$p" ...
   ```

3. Save each response to `$RUN/seederapi-<id>.json` and print only IDs, status, error text and `mangleMap` emails. Responses can hold keys.
4. A guard rejection returns 400. Confirm it wrote no row.
5. Before posting `OrganizationStripeBillingScene` or `UserStripeBillingScene`, follow step 6.1. Each calls Stripe.

### 6. Stripe canary

1. Ask before any `--stripe-billing` run and before posting `OrganizationStripeBillingScene` or `UserStripeBillingScene`. Each creates real customers in the shared Stripe test account; report how many. A scene that fails after the Stripe call names the customer to cancel in its error; report it.
2. `globalSettings.PricingUri` must be set; the billing step has no fallback without it.
3. Seed one small preset, or the regression.md command when no preset reaches the path, with the flag and compare its counts with the plain seed. Check `Gateway=0`, the `cus_` and `sub_` IDs and `ExpirationDate`.
4. An expired test key fails after commit and leaves an orphaned org. Report the org; the developer removes it.

### 7. Web vault

Run the web vault checks in the plan. Drive the browser as [references/web-vault.md](references/web-vault.md) describes.

### 8. SSO login (when in scope)

When SSO is in scope (step 1.5), follow [references/sso-login.md](references/sso-login.md). Seed with the step 2 recipe and copy the "SSO wiring" block from stderr before the `rm`.

### 9. Report and clean up

1. Write the findings report from [references/report-template.md](references/report-template.md), saved where the developer says, and put a timestamp in the file name so a rerun never overwrites an earlier report. Keep its sections in order; write "None." under any with nothing to report.
2. Move screenshots to the developer's notes folder. Delete `$RUN` with `rm -rf`, the temp presets, `.playwright-mcp/*.log` (they hold access-token URLs) and `page-*.yml`; run the deletes under `bash -c`, because under zsh a glob with no match aborts the command. Closing the browser writes one more console file, so delete again after `browser_close`.
3. Seeded orgs stay in the database. Finish with `git status --porcelain` matching the preflight baseline.
