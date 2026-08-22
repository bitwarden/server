# I need a working database with realistic data

> Just wiped my DB, new to the team, or starting fresh. I need something to log into.

## Quick start

```bash
dotnet run -- preset --name dev.playground
```

## What you get

One org whose logins are the roles — `owner@bw.example`, `admin@bw.example`, `custom@bw.example`, `user@bw.example`, all with password `asdfasdfasdf` — plus eight production-realistic colleagues across four groups. Collections use a deliberate permission mix, so each role login sees a meaningfully different vault. No attachments, so no Azurite required. This is what `dev/seed.ps1` seeds by default.

Skip `--mangle` here: the point of this preset is that the logins stay memorable.

## Who this is for

New hires, anyone who just reset their environment, anyone who wants a clean baseline.

## Variations

| Scenario                            | Command                                                                                       |
| ----------------------------------- | --------------------------------------------------------------------------------------------- |
| Attachment coverage (needs Azurite) | `dotnet run -- preset --name qa.enterprise-basic --mangle`                                    |
| Larger org (58 users, 14 groups)    | `dotnet run -- preset --name qa.dunder-mifflin-enterprise-full --mangle`                      |
| Families plan                       | `dotnet run -- preset --name qa.families-basic --mangle`                                      |
| Free plan personal vault            | `dotnet run -- preset --name qa.stark-free-basic --mangle`                                    |
| Just a user, no org                 | `dotnet run -- individual --subscription premium --first-name Jane --last-name Smith --vault` |
