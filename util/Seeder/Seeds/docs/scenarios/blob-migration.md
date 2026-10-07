# I need to verify the SDK V1→V2 blob-encryption migration end-to-end

> I'm working on the blob conversion and need a vault that exercises every `type_data` and payload branch in one account.

## Quick start

```bash
dotnet run -- preset --name individual.blob-migration
```

## What you get

A premium V1 individual user — log in as `blobmigration@individual.example` with password `asdfasdfasdf`. Their personal vault holds one of every cipher type (login, card, identity, secure note, SSH key) plus a passkey, password history, custom fields (text, hidden, boolean, linked), a reprompt item, and a favorite — maximizing coverage of the blob conversion's `type_data` and payload branches.

## Who this is for

Engineers verifying the SDK V1→V2 blob-encryption migration who need a single account that touches every cipher shape.

## Variations

| Scenario                                    | Command                                                          |
| ------------------------------------------- | ---------------------------------------------------------------- |
| Isolated re-run (randomized IDs and emails) | `dotnet run -- preset --name individual.blob-migration --mangle` |

Add `--mangle` when you need to seed the scenario more than once without colliding with a prior run.

For CLI flags, see the [SeederUtility reference](../../../../SeederUtility/README.md). For the full preset catalog, see [presets.md](../presets.md).
