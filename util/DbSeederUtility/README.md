# Bitwarden Database Seeder Utility

A command-line utility for generating and managing test data for Bitwarden databases.

## Overview

DbSeederUtility is an executable wrapper around the Seeder class library that provides a convenient command-line
interface for executing seed-recipes in your local environment.

## Installation

The utility can be built and run as a .NET 8 application:

```
dotnet build
dotnet run -- <command> [options]
```

Or directly using the compiled executable:

```
DbSeeder.exe <command> [options]
```

## Examples

### Generate and load test organization

```bash
# Generate an organization called "seeded" with 10000 users using the @large.test email domain.
# Login using "admin@large.test" with password "asdfasdfasdf"
DbSeeder.exe organization -n seeded -u 10000 -d large.test
```

## Dependencies

This utility depends on:
- The Seeder class library
- CommandDotNet for command-line parsing
- .NET 8.0 runtime
