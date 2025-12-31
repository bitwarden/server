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
```

**Note:** The owner account will be `owner@{domain}` (e.g., `owner@large.test`) with the master password `asdfasdfasdf`

### Generate organization with collections and ciphers

```bash
# Generate an organization with 100 users, 10 collections, and 50 ciphers per collection
DbSeeder.exe organization -N "Test Corp" -U 100 -D testcorp.com -C 10 -I 50
```

**Options:**
- `-N, --Name` - Name of organization
- `-U, --users` - Number of users to generate
- `-D, --domain` - Email domain for users
- `-C, --collections` - Number of collections to generate (default: 0)
- `-I, --ciphers-per-collection` - Number of ciphers per collection (default: 0)

**Cipher Types:** The seeder generates a realistic mix of cipher types:
- 40% Login (username/password credentials)
- 25% Secure Note (text notes)
- 20% Card (payment cards)
- 15% Identity (personal information)

All cipher data is encrypted using the organization's encryption key and uses the Bogus library to generate realistic test data.

## Dependencies

This utility depends on:
- The Seeder class library
- CommandDotNet for command-line parsing
- .NET 8.0 runtime
