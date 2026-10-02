# I'm building a feature that touches collections or permissions

> Setting up permission edge cases manually is tedious and error-prone. I need readOnly, hidePasswords, and manage combos already wired up.

## Quick start

```bash
dotnet run -- preset --name qa.collection-permissions-enterprise --mangle
```

## What you get

An enterprise org with collections pre-configured to cover permission edge cases — combinations of readOnly, hidePasswords, and manage assigned both directly to users and through groups. Owner email is printed in the CLI output, password `asdfasdfasdf`.

This isn't random data. The test data was hand-crafted to exercise the permission paths that break in production.

## Who this is for

Engineers working on collection access, permission inheritance, or group-based authorization.

## Variations

| Scenario                                           | Command                                                                  |
| -------------------------------------------------- | ------------------------------------------------------------------------ |
| Full enterprise org with named folders + favorites | `dotnet run -- preset --name qa.zero-knowledge-labs-enterprise --mangle` |
| Large org (58 users) with groups and collections   | `dotnet run -- preset --name qa.dunder-mifflin-enterprise-full --mangle` |
| Scale with high permission density (2,500 users)   | `dotnet run -- preset --name scale.lg-highperm-tyrell-corp --mangle`     |
