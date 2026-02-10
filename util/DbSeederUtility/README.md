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
# Login using "owner@large.test" with password "asdfasdfasdf"
DbSeeder.exe organization -n seeded -u 10000 -d large.test

# Generate an organization with 5 users and 100 encrypted ciphers
DbSeeder.exe vault-organization -n TestOrg -u 5 -d test.com -c 100

# Generate with Spotify-style collections (tribes, chapters, guilds)
DbSeeder.exe vault-organization -n TestOrg -u 10 -d test.com -c 50 -o Spotify

# Generate a small test organization with ciphers for manual testing
DbSeeder.exe vault-organization -n DevOrg -u 2 -d dev.local -c 10

# Generate an organization using a traditional structure
dotnet run --project DbSeederUtility.csproj -- vault-organization -n Test001 -d test001.com -u 50 -c 1000 -g 15 -o Traditional -m

# Generate an organization using a modern structure with a small vault
dotnet run --project DbSeederUtility.csproj -- vault-organization -n Test002 -d test002.com -u 500 -c 10000 -g 85 -o Modern -m

# Generate an organization using a spotify structure with a large vault
dotnet run --project DbSeederUtility.csproj -- vault-organization -n Test003 -d test003.com -u 8000 -c 100000 -g 125 -o Spotify -m

# Generate an organization using a traditional structure with a very small vault with European regional data
dotnet run --project DbSeederUtility.csproj  -- vault-organization -n “TestOneEurope” -u 10 -c 100 -g 5 -d testOneEurope.com -o Traditional --region Europe

# Generate an organization using a traditional structure with a very small vault with Asia Pacific regional data
dotnet run --project DbSeederUtility.csproj  -- vault-organization -n “TestOneAsiaPacific” -u 17 -c 600 -g 12 -d testOneAsiaPacific.com -o Traditional --region AsiaPacific

```

## Dependencies

This utility depends on:

- The Seeder class library
- CommandDotNet for command-line parsing
- .NET 8.0 runtime
