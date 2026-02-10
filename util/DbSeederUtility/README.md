# Bitwarden Database Seeder Utility

A command-line utility for generating and managing test data for Bitwarden databases.

## Overview

DbSeederUtility is an executable wrapper around the Seeder class library that provides a convenient command-line
interface for executing seed-recipes in your local environment. It supports both fixture-based seeding (realistic data) and procedural generation.

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

## Commands

### seed - Fixture-Based Seeding (NEW)

Load realistic test data from embedded preset fixtures.

```bash
# List all available presets and fixtures
DbSeeder.exe seed --list

# Load an embedded preset
DbSeeder.exe seed --preset dunder-mifflin-full

# Enable ID mangling for test isolation
DbSeeder.exe seed --preset dunder-mifflin-full --mangle
```

**Available Presets:**
- `dunder-mifflin-full` - Complete Dunder Mifflin organization with 58 users, 14 groups, 15 collections, and test ciphers
- `large-enterprise` - Large organization structure for performance testing

**Login Credentials:**
- Owner: `owner@<domain>` (e.g., `michael.scott@dundermifflin.com` for Dunder Mifflin)
- Password: `asdfasdfasdf` (all users)

### organization - Simple Organization Seeding

Generate a basic organization with users (no vault data).

```bash
DbSeeder.exe organization -n MyOrg -u 100 -d myorg.com
```

### vault-organization - Procedural Vault Seeding

Generate an organization with users and encrypted vault data using procedural generation.

## Examples

### Fixture-Based Seeding (Realistic Data)

```bash
# Load Dunder Mifflin complete dataset (58 users, realistic org structure)
DbSeeder.exe seed --preset dunder-mifflin-full

# List all available presets and fixtures
DbSeeder.exe seed --list
```

### Procedural Generation (Synthetic Data)

```bash
# Generate an organization called "seeded" with 10000 users
DbSeeder.exe organization -n seeded -u 10000 -d large.test

# Generate an organization with 5 users and 100 encrypted ciphers
DbSeeder.exe vault-organization -n TestOrg -u 5 -d test.com -c 100

# Generate with Spotify-style collections (tribes, chapters, guilds)
DbSeeder.exe vault-organization -n TestOrg -u 10 -d test.com -c 50 -o Spotify

# Generate a small test organization with ciphers for manual testing
DbSeeder.exe vault-organization -n DevOrg -u 2 -d dev.local -c 10

# Generate with traditional org structure and European regional data
DbSeeder.exe vault-organization -n TestEurope -u 10 -c 100 -g 5 -d testeurope.com -o Traditional --region Europe

# Generate large organization with Asia Pacific regional data
DbSeeder.exe vault-organization -n TestAsiaPacific -u 17 -c 600 -g 12 -d testasiapacific.com --region AsiaPacific
```

## Dependencies

This utility depends on:

- The Seeder class library
- CommandDotNet for command-line parsing
- .NET 8.0 runtime
