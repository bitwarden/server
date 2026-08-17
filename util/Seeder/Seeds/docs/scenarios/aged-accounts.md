# How do I test behavior that depends on how old an account is?

> I need a user whose account was created weeks or months ago, not just now.

## Quick start

```bash
dotnet run -- individual --subscription free --account-age-days 365
```

## What you get

A standalone individual user (password `asdfasdfasdf`) whose `CreationDate` is backdated by the given number of days. Only `CreationDate` is backdated; `RevisionDate` and `AccountRevisionDate` stay at the seed time, matching a long-lived account that was just touched.

## Who this is for

Engineers testing account-age-gated behavior, retention or dormancy windows, or any flow that branches on how long ago an account was created.

## Variations

Combine with `--vault` for personal vault data, `--email` for a predictable address, or `--subscription premium` for a premium aged account. See the [SeederUtility reference](../../../../SeederUtility/README.md) for all `individual` flags.
