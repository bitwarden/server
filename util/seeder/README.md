# Bitwarden Database Seeder

A class library for generating, loading, and extracting test data for Bitwarden databases.

## Overview

The Seeder library provides functionality to:

1. **Generate** realistic test data for Bitwarden, including users and ciphers
2. **Load** previously generated test data into the database
3. **Extract** existing data from a database into seed files
4. **Generate and load** data in a single operation

## Project Structure

The project is organized into these main components:

### Commands

- **ExtractCommand** - Extracts existing data from the database into seed files
- **GenerateCommand** - Generates new test data as seed files or directly loads it
- **LoadCommand** - Loads previously generated seed files into the database

### Services

- **DatabaseContext** - EF Core DbContext connecting to the configured database
- **DatabaseService** - Provides database operations (save, retrieve, clear data)
- **EncryptionService** - Handles security operations (password hashing, encryption)
- **SeederService** - Core service that generates realistic test data using Bogus

### Settings

- **GlobalSettings** - Configuration model for database connections
- **GlobalSettingsFactory** - Loads and caches settings from config sources

## Usage

The Seeder library is designed to be used by the DbSeederUtility executable. For direct usage in code:

```csharp
// Get seeder service from dependency injection
var seederService = serviceProvider.GetRequiredService<ISeederService>();

// Generate seed data
await seederService.GenerateSeedsAsync(
    userCount: 10, 
    ciphersPerUser: 5, 
    seedName: "test_data"
);

// Load seed data
await seederService.LoadSeedsAsync(
    seedName: "test_data",
    timestamp: null // Use most recent if null
);

// Extract data
await seederService.ExtractSeedsAsync(
    seedName: "extracted_data"
);

// Generate and load in one step
await seederService.GenerateAndLoadSeedsAsync(
    userCount: 10,
    ciphersPerUser: 5,
    seedName: "direct_load"
);
```

## Configuration

The library uses the following configuration sources (in order of precedence):

1. Environment variables
2. User secrets (with ID "Bit.Seeder")
3. appsettings.{Environment}.json
4. appsettings.json

The expected configuration structure is:

```json
{
  "globalSettings": {
    "selfHosted": true,
    "databaseProvider": "postgres",
    "sqlServer": {
      "connectionString": "..."
    },
    "postgreSql": {
      "connectionString": "..."
    },
    "mySql": {
      "connectionString": "..."
    },
    "sqlite": {
      "connectionString": "..."
    }
  }
}
```

## Seed File Structure

Seed files are organized as follows:

```
seeds/
├── {seed_name}/
│   └── {timestamp}/
│       ├── users/
│       │   └── users.json
│       └── ciphers/
│           ├── {user_id1}.json
│           ├── {user_id2}.json
│           └── ...
```

## Dependencies

- **EntityFrameworkCore** - Database access
- **Bogus** - Realistic test data generation
- **CommandDotNet** - Used by DbSeederUtility for CLI commands
- **DataProtection** - Used for secure data handling

## Best Practices

- Clear the database before loading new seed data
- Use consistent seed names for related operations
- Store sensitive connection strings in user secrets or environment variables
- Use the DbSeederUtility executable for command-line operations 