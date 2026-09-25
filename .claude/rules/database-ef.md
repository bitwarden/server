---
paths:
  - "util/MySqlMigrations/**"
  - "util/PostgresMigrations/**"
  - "util/SqliteMigrations/**"
  - "src/Infrastructure.EntityFramework/**"
---

# EF Core migrations (PostgreSQL, MySQL, SQLite)

Bitwarden supports four databases through two ORMs. EF Core serves PostgreSQL, MySQL, and SQLite; SQL Server uses
Dapper. **A schema change must be reflected in both tracks** — after changing anything here, make the matching MSSQL
change (see the `database-dapper` rule). Skipping either silently breaks one of the four supported databases. For
architecture, the dual-ORM split, and Evolutionary Database Design strategy, defer to the `writing-database-queries`
and `implementing-ef-core` skills.

## Never hand-write generated files

**NEVER** hand-write an EF migration `.cs` file or its `.Designer.cs` / `DatabaseContextModelSnapshot.cs` snapshot.
These are machine-generated; hand-editing them corrupts the model snapshot and breaks future migrations. If the tooling
can't run (e.g. Docker isn't up), fix the tooling — do not write the file by hand.

## Generating a migration

**Always:**

1. Update the entity class in `src/Infrastructure.EntityFramework/` first.
2. Start MySQL so the design-time factory can probe the live server for its version:
   ```
   docker compose --profile mysql up -d
   ```
3. Generate migrations for all three providers and refresh each `DatabaseContextModelSnapshot.cs`:
   ```
   pwsh dev/ef_migrate.ps1 <MigrationName>
   ```

To regenerate just one provider: `dotnet ef migrations add <Name> -s util/MySqlMigrations`.