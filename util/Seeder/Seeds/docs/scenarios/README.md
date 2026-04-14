# Seeder Scenarios

Not sure what to run? Start here. Each scenario starts with a problem and ends with a command.

**The Seeder writes directly to your database. Only run it against local development databases.**

All commands run from `util/SeederUtility/`. All seeded users use password `asdfasdfasdf` (override with `--password`). Use `--mangle` for test isolation (randomizes IDs and emails so repeated runs don't collide).

For CLI flag details, see the [SeederUtility reference](../../../../SeederUtility/README.md). For the full preset catalog, see [presets.md](../presets.md).

## Scenarios

| Scenario                                    | Problem                                                        | Command type   |
| ------------------------------------------- | -------------------------------------------------------------- | -------------- |
| [Fresh database](fresh-database.md)         | I need a working database with realistic data                  | `preset`       |
| [Permission testing](permission-testing.md) | I'm building a feature that touches collections or permissions | `preset`       |
| [Scale testing](scale-testing.md)           | Will my code survive at 250 / 1,000 / 5,000+ users?            | `preset`       |
| [Bug reproduction](bug-reproduction.md)     | I need to reproduce a customer's org shape                     | `organization` |

## Contributing a Scenario

If you solved a problem with the Seeder that isn't listed here, add it. One file per scenario.

1. Copy the template below into a new file in this folder (kebab-case name)
2. Fill it in
3. Add a row to the table above
4. Open a PR

### Template

```markdown
# [Problem as a question]

> One-sentence pain statement.

## Quick start

\`\`\`bash
dotnet run -- <your command here>
\`\`\`

## What you get

What's in the database after this runs. Entity counts, login credentials, notable structure.

## Who this is for

Engineers who feel this pain.

## Variations

Other flags or presets worth trying for this problem.
```

## Field Test Feedback

Tried the Seeder and have feedback? We want to hear it:

- What did you try?
- What worked?
- What was confusing or missing?
- What scenario do you wish existed?

Open an issue or start a thread in Slack.
