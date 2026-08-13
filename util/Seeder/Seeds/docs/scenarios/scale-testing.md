# Will my code survive at 250 / 1,000 / 5,000+ users?

> "I think this will scale, but I have no way to verify."

## Quick start

Pick the tier that matches your concern:

```bash
# 250 users, 50 groups, 500 collections, 5K ciphers
dotnet run -- preset --name scale.md-balanced-sterling-cooper --mangle

# 1,000 users, 100 groups, 2K collections, 10K ciphers
dotnet run -- preset --name scale.lg-balanced-wayne-enterprises --mangle

# 5,000 users, 500 groups, 1.2K collections, 15K ciphers
dotnet run -- preset --name scale.xl-highperm-weyland-yutani --mangle
```

## What you get

These presets go beyond row counts. They model realistic group sizes (a few large groups, many small ones), varied permission types (readOnly, manage, hidePasswords), unassigned ciphers, and items belonging to multiple collections. The data shape mirrors real enterprise customers.

This is the difference between "25K users with 5 groups" (useless) and "250 users with 50 groups across 500 collections where group sizes vary realistically" (the bug finder).

The owner email is printed in the CLI output — log in with password `asdfasdfasdf`.

## Who this is for

Anyone touching queries, list views, API endpoints, or anything that multiplies across users, groups, and collections.

## Variations

The full scale catalog is in [presets.md](../presets.md#scale). Other shapes worth knowing:

| Shape                                        | Preset                                  | Why                                            |
| -------------------------------------------- | --------------------------------------- | ---------------------------------------------- |
| Collection-heavy (800 collections, 8 groups) | `scale.md-highcollection-umbrella-corp` | Few groups managing many collections              |
| High permission density                      | `scale.lg-highperm-tyrell-corp`         | 2,500 users with heavily skewed permissions       |
| Mega corp, many collections                  | `scale.xl-broad-initech`                | 10K users, 12K collections, most ciphers unassigned |
