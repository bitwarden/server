# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

DbSeederUtility is the CLI wrapper for the Seeder library. It exposes seed-recipes as command-line commands using CommandDotNet.

**For Seeder library patterns (Factories, Recipes, Models, etc.), read `/util/Seeder/CLAUDE.md`.**

## Commands

```bash
# Build
dotnet build util/DbSeederUtility/DbSeederUtility.csproj

# Run (from repo root)
dotnet run --project util/DbSeederUtility -- <command> [options]

# Get help
dotnet run --project util/DbSeederUtility -- --help
dotnet run --project util/DbSeederUtility -- vault-organization --help
```

## Architecture

| File | Purpose |
|------|---------|
| `Program.cs` | CommandDotNet entry point. Each `[Command]` method maps to a Recipe |
| `*Args.cs` | CLI argument models implementing `IArgumentModel`. Validate input and convert to `*Options` |
| `ServiceCollectionExtension.cs` | DI setup for database context, AutoMapper, password hasher |

## Adding a New Command

1. Create `{CommandName}Args.cs` with CLI options using `[Option]` attributes
2. Add `Validate()` method for input validation
3. Add `ToOptions()` method to convert to Seeder library options type
4. Add `[Command]` method in `Program.cs` that instantiates the Recipe and calls `Seed()`

## CLI Examples

See `README.md` for full usage examples. Default login: `owner@{domain}` with password `asdfasdfasdf`.
