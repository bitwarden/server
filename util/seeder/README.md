# Bitwarden Database Seeder

The Bitwarden Database Seeder utility is a tool for generating and loading test data for Bitwarden databases.

## Features

- Generate random user accounts with associated ciphers (passwords, secure notes, etc.)
- Load generated seeds into the database
- Configure database connections using the same configuration approach as the Bitwarden Migrator

## Usage

The seeder utility can be run in two ways:

1. Using `dotnet run` from the source:
   ```
   cd util/DbSeederUtility
   dotnet run -- generate-seeds --users 10 --ciphers 5 --output ./seeds
   dotnet run -- load-seeds --path ./seeds
   ```

2. As a standalone executable:
   ```
   DbSeeder.exe generate-seeds --users 10 --ciphers 5 --output ./seeds
   DbSeeder.exe load-seeds --path ./seeds
   ```

## Commands

### Generate Seeds

Generates random seed data for users and ciphers.

```
DbSeeder.exe generate-seeds [options]

Options:
  -u, --users <NUMBER>     Number of users to generate (default: 10)
  -c, --ciphers <NUMBER>   Number of ciphers per user (default: 5)
  -o, --output <DIRECTORY> Output directory for seed files (default: seeds)
```

### Load Seeds

Loads generated seed data into the database.

```
DbSeeder.exe load-seeds [options]

Options:
  -p, --path <DIRECTORY>   Path to the directory containing seed data
  --dry-run                Preview the operation without making changes
```

## Configuration

The utility uses the same configuration approach as other Bitwarden utilities:

1. **User Secrets** - For local development
2. **appsettings.json** - For general settings
3. **Environment variables** - For deployment environments

### Database Configuration

Configure the database connection in user secrets or appsettings.json:

```json
{
  "globalSettings": {
    "databaseProvider": "postgresql", // or "sqlserver", "mysql", "sqlite"
    "postgreSql": {
      "connectionString": "Host=localhost;Port=5432;Database=vault_dev;Username=postgres;Password=YOURPASSWORD;Include Error Detail=true"
    },
    "sqlServer": {
      "connectionString": "Data Source=localhost;Initial Catalog=vault_dev;Integrated Security=SSPI;MultipleActiveResultSets=true"
    }
  }
}
```

## Building the Executable

To build the standalone executable:

```
cd util
.\publish-seeder.ps1
```

This will create the executable in the `util/Seeder/publish/win-x64` directory. You can also specify a different runtime:

```
.\publish-seeder.ps1 -runtime linux-x64
```

## Integration with Bitwarden Server

The seeder utility is designed to work seamlessly with Bitwarden Server. It uses the same database models and configuration approach as the server, ensuring compatibility with the core repository. 