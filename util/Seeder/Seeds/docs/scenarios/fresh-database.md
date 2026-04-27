# I need a working database with realistic data

> Just wiped my DB, new to the team, or starting fresh. I need something to log into.

## Quick start

```bash
dotnet run -- preset --name qa.enterprise-basic --mangle
```

## What you get

A standard enterprise org with known users, groups, collections, and encrypted vault items. The owner email is printed in the CLI output — log in with password `asdfasdfasdf`.

## Who this is for

New hires, anyone who just reset their environment, anyone who wants a clean baseline.

## Variations

| Scenario                         | Command                                                                                       |
| -------------------------------- | --------------------------------------------------------------------------------------------- |
| Larger org (58 users, 14 groups) | `dotnet run -- preset --name qa.dunder-mifflin-enterprise-full --mangle`                      |
| Families plan                    | `dotnet run -- preset --name qa.families-basic --mangle`                                      |
| Free plan personal vault         | `dotnet run -- preset --name qa.stark-free-basic --mangle`                                    |
| Just a user, no org              | `dotnet run -- individual --subscription premium --first-name Jane --last-name Smith --vault` |
