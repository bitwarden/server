# I need to reproduce a customer's org shape

> Bug report says "200 users, 800 collections, 8 groups." Can't repro locally.

## Quick start

```bash
dotnet run -- organization -n BugRepro -d repro.example -u 200 -c 500 --collections 800 -g 8 --mangle
```

> Only run the Seeder against local development databases. Use fictional domains and round numbers — do not replicate exact customer details.

Tweak the numbers to match the customer's profile. The `organization` command gives you full control over user count (`-u`), cipher count (`-c`), group count (`-g`), org structure (`-o`), region, and plan type.

## What you get

An org approximating the customer's shape with encrypted vault data, group memberships, and collection assignments. The owner email is printed in the CLI output — log in with password `asdfasdfasdf`.

## Who this is for

Any engineer reproducing a customer-reported issue.

## Variations

| Scenario                                   | Command                                                               |
| ------------------------------------------ | --------------------------------------------------------------------- |
| Match a specific plan type                 | Add `--plan-type teams-annually` or `--plan-type enterprise-annually` |
| No vault data (just users + org structure) | Omit `-c` — e.g., `-n BugRepro -d repro.example -u 200 -g 8`          |
| Specific org structure                     | Add `-o Traditional`, `-o Spotify`, or `-o Modern`                    |
| Production-realistic auth for e2e          | Add `--kdf-iterations 600000`                                         |
