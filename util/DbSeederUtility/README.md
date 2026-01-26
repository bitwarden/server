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
DbSeeder.exe organization -n TestOrg -u 5 -d test.com -c 100

# Generate a small test organization with ciphers for manual testing
DbSeeder.exe organization -n DevOrg -u 2 -d dev.local -c 10
```

### Options

| Option | Description |
|--------|-------------|
| `-n, --name` | Organization name |
| `-u, --users` | Number of member users to create |
| `-d, --domain` | Email domain (e.g., test.com creates owner@test.com) |
| `-c, --ciphers` | Number of encrypted ciphers to create (optional) |
| `-s, --status` | User status: Confirmed (default), Invited, Accepted, Revoked |

### Notes

- All users are created with the password `asdfasdfasdf`
- The owner account is always `owner@{domain}` with Confirmed status
- Member accounts are `user0@{domain}`, `user1@{domain}`, etc.
- When ciphers are created, a "Default Collection" is automatically created and all users are granted access
- Ciphers are encrypted using dynamically generated organization keys

## Dependencies

This utility depends on:
- The Seeder class library
- CommandDotNet for command-line parsing
- .NET 8.0 runtime
