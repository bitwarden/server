# Bitwarden Database Seeder Utility

A command-line utility for generating and managing test data for Bitwarden databases.

## Overview

DbSeederUtility is an executable wrapper around the Seeder class library that provides a convenient command-line interface for:

1. **Generating** test data as JSON files
2. **Loading** test data into a database
3. **Extracting** database data into seed files
4. **Generating and loading** data in a single operation

## Commands

The utility provides the following commands:

### generate

Generates seed data as JSON files.

```
DbSeeder.exe generate --users <count> --ciphers-per-user <count> --seed-name <name>
```

Options:
- `-u, --users`: Number of users to generate
- `-c, --ciphers-per-user`: Number of ciphers per user to generate
- `-n, --seed-name`: Name for the seed data files

Example:
```
DbSeeder.exe generate --users 10 --ciphers-per-user 5 --seed-name test_data
```

### load

Loads seed data from JSON files into the database.

```
DbSeeder.exe load --seed-name <name> [--timestamp <timestamp>] [--dry-run]
```

Options:
- `-n, --seed-name`: Name of the seed data to load
- `-t, --timestamp`: Specific timestamp of the seed data to load (defaults to most recent)
- `-d, --dry-run`: Validate the seed data without actually loading it

Example:
```
DbSeeder.exe load --seed-name test_data
```

### generate-direct-load

Generates seed data and loads it directly into the database without creating JSON files.

```
DbSeeder.exe generate-direct-load --users <count> --ciphers-per-user <count> --seed-name <name>
```

Options:
- `-u, --users`: Number of users to generate
- `-c, --ciphers-per-user`: Number of ciphers per user to generate
- `-n, --seed-name`: Name identifier for this seed operation

Example:
```
DbSeeder.exe generate-direct-load --users 3 --ciphers-per-user 5 --seed-name direct_test_data
```

### extract

Extracts data from the database into seed files.

```
DbSeeder.exe extract --seed-name <name>
```

Options:
- `-n, --seed-name`: Name for the extracted seed

Example:
```
DbSeeder.exe extract --seed-name extracted_data
```

## Configuration

DbSeederUtility uses the same configuration as the Seeder library. See the Seeder README for details on configuration options and file structure.

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

### Generate and load test data

```bash
# Generate 10 users, each with 5 ciphers
DbSeeder.exe generate --users 10 --ciphers-per-user 5 --seed-name demo_data

# Load the generated data
DbSeeder.exe load --seed-name demo_data
```

### Extract and reload data

```bash
# Extract existing data
DbSeeder.exe extract --seed-name production_backup

# Load the extracted data
DbSeeder.exe load --seed-name production_backup
```

### One-step generation and loading

```bash
# Generate and load in one step
DbSeeder.exe generate-direct-load --users 5 --ciphers-per-user 10 --seed-name quick_test
```

## Dependencies

This utility depends on:
- The Seeder class library
- CommandDotNet for command-line parsing
- .NET 8.0 runtime 