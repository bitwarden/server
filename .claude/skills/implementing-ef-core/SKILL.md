---
name: implementing-ef-core
description: Implementing Entity Framework Core repositories and migrations for PostgreSQL, MySQL, and SQLite at Bitwarden. Use when creating or modifying EF repositories, generating EF migrations, or working with non-MSSQL data access in the server repo. Also use when editing `EntityTypeConfiguration<T>` classes or debugging provider-specific LINQ translation issues.
---

## Repository Pattern

EF implementations live in `src/Infrastructure/EntityFramework/Repositories/`. Each class implements the same interface as its Dapper counterpart. The EF repository uses `DbContext` and LINQ queries instead of stored procedures, but must produce identical behavior.

### Why behavior must match stored procedures exactly

Bitwarden self-hosted runs on the customer's choice of database. If `CipherRepository.GetManyByUserId()` returns results in a different order on PostgreSQL than the stored procedure returns on MSSQL, or filters differently, or handles nulls differently — that's a bug. Users switching databases or comparing behavior across environments will see inconsistencies.

The `[DatabaseData]` integration test attribute runs the same test against all configured databases. This is the primary safety net for parity.

### Cross-database considerations

EF Core's LINQ-to-SQL translation varies by provider. Patterns that work on one database may fail on another:

- **PostgreSQL** is stricter about types — operations like `Min()` on booleans or implicit string/int conversions that MySQL allows will throw
- **SQLite** has limited ALTER TABLE support — some migrations that work elsewhere fail on SQLite
- **Case sensitivity** depends on database collation, not on C# code — don't assume case-insensitive string comparison

The pragmatic approach: write clean LINQ, run `[DatabaseData]` tests, and fix provider-specific failures as they surface rather than trying to predict every edge case.

## Migration Generation

### Workflow

Run `pwsh ef_migrate.ps1 <MigrationName>` to generate migrations for all EF targets simultaneously. This creates migration files for each provider (PostgreSQL, MySQL, SQLite).

### Why the migration name matters

The EF migration class name must exactly match the MSSQL migration name portion (from the `YYYY-MM-DD_##_MigrationName.sql` filename). This convention keeps migration history aligned across ORMs and makes it easy to trace which EF migration corresponds to which SQL script.

### Always review generated migrations

EF's migration generator makes mechanical decisions that aren't always optimal:

- It may drop and recreate indexes instead of renaming them
- It may generate unnecessary column modifications when model annotations change
- It doesn't know about Bitwarden's large table concerns (never add indexes to `Cipher`, `OrganizationUser` etc. without careful review)

Review the generated `Up()` and `Down()` methods to ensure they align with the stored procedure migration's intent.

## Key Decisions That Trip Up AI Assistants

### Don't add navigation properties casually

EF navigation properties (e.g., `public virtual Organization Organization { get; set; }`) affect query generation and lazy loading behavior. Only add them when the stored procedure equivalent also joins those tables. Unnecessary navigation properties cause N+1 queries that don't match the stored procedure's behavior.

### DbContext configuration lives in `EntityTypeConfiguration` classes

Don't configure entities inline in `OnModelCreating`. Each entity has a configuration class that defines table mapping, relationships, and constraints. This keeps the DbContext clean and each entity's configuration self-contained.

### Respect the same GUID generation strategy

Entity IDs are generated in application code via `CoreHelpers.GenerateComb()`, not by the database. Don't configure `ValueGeneratedOnAdd()` or database-generated defaults for ID columns in EF configuration.

## Critical Rules

These are the most frequently violated conventions. Claude cannot fetch the linked docs at runtime, so these are inlined here:

- **One `EntityTypeConfiguration<T>` class per entity** — never configure inline in `OnModelCreating`
- **Migration name must match MSSQL migration name** from `YYYY-MM-DD_##_MigrationName.sql`
- **Run `pwsh ef_migrate.ps1 <Name>`** to generate migrations for all providers simultaneously
- **Review `Up()` and `Down()` methods** in every generated migration before committing
- **No `ValueGeneratedOnAdd()` on ID columns** — IDs come from `CoreHelpers.GenerateComb()` in app code

## Examples

### GUID configuration

```csharp
// CORRECT — ID generated in application code
public void Configure(EntityTypeBuilder<Cipher> builder)
{
    builder.HasKey(c => c.Id);
    // No ValueGeneratedOnAdd — CoreHelpers.GenerateComb() handles this
}

// WRONG — lets database generate IDs, breaks MSSQL parity
public void Configure(EntityTypeBuilder<Cipher> builder)
{
    builder.HasKey(c => c.Id);
    builder.Property(c => c.Id).ValueGeneratedOnAdd();
}
```

### Navigation properties

```csharp
// CORRECT — only add when the SP also joins this table
public class Cipher
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    // No navigation property — the SP doesn't JOIN Organization
}

// WRONG — causes N+1 queries that don't match SP behavior
public class Cipher
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public virtual Organization Organization { get; set; }
}
```

## Further Reading

- [Database migrations (EF)](https://contributing.bitwarden.com/contributing/database-migrations/ef)
- [SQL code style](https://contributing.bitwarden.com/contributing/code-style/sql/)
