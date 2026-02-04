# Replace SetupIntent Cache Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Use superpowers:test-driven-development for all code writing.

**Goal:** Replace the SetupIntentDistributedCache with a customer-based approach that queries Stripe and repositories by GatewayCustomerId.

**Architecture:** Set the `customer` field on SetupIntents when retrieved, query repositories by `GatewayCustomerId` in webhooks, and remove all cache-related code plus dead code paths.

**Tech Stack:** C#/.NET, SQL Server (Dapper), Entity Framework Core, xUnit, NSubstitute

**Implementation Guidelines:**
1. Use `/superpowers:test-driven-development` when writing code
2. Logical git commits - not too small, not too big; group related changes
3. Use conventional commits format (e.g., `feat:`, `refactor:`, `chore:`, `test:`)
4. Never use "Co-Authored-By: Claude..." in commit messages
5. Ask questions when stuck rather than guessing

**Reference:** See `docs/plans/2026-02-04-replace-setup-intent-cache-design.md` for full design context.

---

## Table of Contents

- [Phase 1: Database Infrastructure](#phase-1-database-infrastructure)
  - [Task 1: Create Stored Procedure Files](#task-1-create-stored-procedure-files)
  - [Task 2: Update Table Definition Files with Indexes](#task-2-update-table-definition-files-with-indexes)
  - [Task 3: Create Migration Script](#task-3-create-migration-script)
  - [Task 4: Build and Verify SQL Changes](#task-4-build-and-verify-sql-changes)
- [Phase 2: Repository Interface and Dapper Implementation](#phase-2-repository-interface-and-dapper-implementation)
  - [Task 5: Add IOrganizationRepository Interface Methods and Dapper Implementation](#task-5-add-iorganizationrepository-interface-methods-and-dapper-implementation)
  - [Task 6: Add IProviderRepository Interface Methods and Dapper Implementation](#task-6-add-iproviderrepository-interface-methods-and-dapper-implementation)
  - [Task 7: Add IUserRepository Interface Methods and Dapper Implementation](#task-7-add-iuserrepository-interface-methods-and-dapper-implementation)
- [Phase 3: Entity Framework Implementation](#phase-3-entity-framework-implementation)
  - [Task 8: Add EF OrganizationRepository Methods and Index Configuration](#task-8-add-ef-organizationrepository-methods-and-index-configuration)
  - [Task 9: Add EF ProviderRepository Methods and Index Configuration](#task-9-add-ef-providerrepository-methods-and-index-configuration)
  - [Task 10: Add EF UserRepository Methods and Index Configuration](#task-10-add-ef-userrepository-methods-and-index-configuration)
  - [Task 11: Generate EF Migrations](#task-11-generate-ef-migrations)
- [Phase 4: Update SetupIntent Handling](#phase-4-update-setupintent-handling)
  - [Task 12: Update SetupIntentSucceededHandler](#task-12-update-setupintentsucceededhandler)
  - [Task 13: Update StripeEventService](#task-13-update-stripeeventservice)
  - [Task 14: Update GetPaymentMethodQuery](#task-14-update-getpaymentmethodquery)
  - [Task 15: Update HasPaymentMethodQuery](#task-15-update-haspaymentmethodquery)
- [Phase 5: Update SetupIntent Customer Assignment](#phase-5-update-setupintent-customer-assignment)
  - [Task 16: Update OrganizationBillingService.CreateCustomerAsync](#task-16-update-organizationbillingservicecreatecustomerasync)
  - [Task 17: Update ProviderBillingService.SetupCustomer](#task-17-update-providerbillingservicesetupcustomer)
  - [Task 18: Update ProviderBillingService.SetupSubscription](#task-18-update-providerbillingservicesetupsubscription)
  - [Task 19: Update UpdatePaymentMethodCommand](#task-19-update-updatepaymentmethodcommand)
- [Phase 6: Remove Dead Code](#phase-6-remove-dead-code)
  - [Task 20: Remove Bank Account Case from CreatePremiumCloudHostedSubscriptionCommand](#task-20-remove-bank-account-case-from-createpremiumcloudhostedsubscriptioncommand)
  - [Task 21: Remove SubscriberService.UpdatePaymentSource](#task-21-remove-subscriberserviceupdatepaymentsource)
  - [Task 22: Remove OrganizationBillingService.UpdatePaymentMethod](#task-22-remove-organizationbillingserviceupdatepaymentmethod)
  - [Task 23: Remove ProviderBillingService.UpdatePaymentMethod](#task-23-remove-providerbillingserviceupdatepaymentmethod)
  - [Task 24: Remove PremiumUserBillingService Dead Methods](#task-24-remove-premiumuserbillingservice-dead-methods)
  - [Task 25: Remove UserService Dead Methods](#task-25-remove-userservice-dead-methods)
- [Phase 7: Remove Cache Infrastructure](#phase-7-remove-cache-infrastructure)
  - [Task 26: Remove ISetupIntentCache and Implementation](#task-26-remove-isetupintentcache-and-implementation)
- [Phase 8: Final Verification](#phase-8-final-verification)
  - [Task 27: Run Full Test Suite](#task-27-run-full-test-suite)
  - [Task 28: Final Review](#task-28-final-review)

---

## Phase 1: Database Infrastructure

### Task 1: Create Stored Procedure Files

**Files:**
- Create: `src/Sql/dbo/Stored Procedures/Organization_ReadByGatewayCustomerId.sql`
- Create: `src/Sql/dbo/Stored Procedures/Organization_ReadByGatewaySubscriptionId.sql`
- Create: `src/Sql/dbo/Stored Procedures/Provider_ReadByGatewayCustomerId.sql`
- Create: `src/Sql/dbo/Stored Procedures/Provider_ReadByGatewaySubscriptionId.sql`
- Create: `src/Sql/dbo/Stored Procedures/User_ReadByGatewayCustomerId.sql`
- Create: `src/Sql/dbo/Stored Procedures/User_ReadByGatewaySubscriptionId.sql`

**Step 1: Create Organization_ReadByGatewayCustomerId.sql**

```sql
CREATE PROCEDURE [dbo].[Organization_ReadByGatewayCustomerId]
    @GatewayCustomerId VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationView]
    WHERE
        [GatewayCustomerId] = @GatewayCustomerId
END
```

**Step 2: Create Organization_ReadByGatewaySubscriptionId.sql**

```sql
CREATE PROCEDURE [dbo].[Organization_ReadByGatewaySubscriptionId]
    @GatewaySubscriptionId VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationView]
    WHERE
        [GatewaySubscriptionId] = @GatewaySubscriptionId
END
```

**Step 3: Create Provider_ReadByGatewayCustomerId.sql**

```sql
CREATE PROCEDURE [dbo].[Provider_ReadByGatewayCustomerId]
    @GatewayCustomerId VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[ProviderView]
    WHERE
        [GatewayCustomerId] = @GatewayCustomerId
END
```

**Step 4: Create Provider_ReadByGatewaySubscriptionId.sql**

```sql
CREATE PROCEDURE [dbo].[Provider_ReadByGatewaySubscriptionId]
    @GatewaySubscriptionId VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[ProviderView]
    WHERE
        [GatewaySubscriptionId] = @GatewaySubscriptionId
END
```

**Step 5: Create User_ReadByGatewayCustomerId.sql**

```sql
CREATE PROCEDURE [dbo].[User_ReadByGatewayCustomerId]
    @GatewayCustomerId VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[UserView]
    WHERE
        [GatewayCustomerId] = @GatewayCustomerId
END
```

**Step 6: Create User_ReadByGatewaySubscriptionId.sql**

```sql
CREATE PROCEDURE [dbo].[User_ReadByGatewaySubscriptionId]
    @GatewaySubscriptionId VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[UserView]
    WHERE
        [GatewaySubscriptionId] = @GatewaySubscriptionId
END
```

**Step 7: Commit**

```bash
git add "src/Sql/dbo/Stored Procedures/Organization_ReadByGatewayCustomerId.sql" \
        "src/Sql/dbo/Stored Procedures/Organization_ReadByGatewaySubscriptionId.sql" \
        "src/Sql/dbo/Stored Procedures/Provider_ReadByGatewayCustomerId.sql" \
        "src/Sql/dbo/Stored Procedures/Provider_ReadByGatewaySubscriptionId.sql" \
        "src/Sql/dbo/Stored Procedures/User_ReadByGatewayCustomerId.sql" \
        "src/Sql/dbo/Stored Procedures/User_ReadByGatewaySubscriptionId.sql"
git commit -m "feat(db): add gateway lookup stored procedures for Organization, Provider, and User"
```

---

### Task 2: Update Table Definition Files with Indexes

**Files:**
- Modify: `src/Sql/dbo/Tables/Organization.sql`
- Modify: `src/Sql/dbo/Tables/Provider.sql`
- Modify: `src/Sql/dbo/Tables/User.sql`

**Step 1: Add indexes to Organization.sql**

Add at the end of the file (after existing indexes):

```sql
GO
CREATE NONCLUSTERED INDEX [IX_Organization_GatewayCustomerId]
    ON [dbo].[Organization]([GatewayCustomerId])
    WHERE [GatewayCustomerId] IS NOT NULL;

GO
CREATE NONCLUSTERED INDEX [IX_Organization_GatewaySubscriptionId]
    ON [dbo].[Organization]([GatewaySubscriptionId])
    WHERE [GatewaySubscriptionId] IS NOT NULL;
```

**Step 2: Add indexes to Provider.sql**

Add at the end of the file:

```sql

GO
CREATE NONCLUSTERED INDEX [IX_Provider_GatewayCustomerId]
    ON [dbo].[Provider]([GatewayCustomerId])
    WHERE [GatewayCustomerId] IS NOT NULL;

GO
CREATE NONCLUSTERED INDEX [IX_Provider_GatewaySubscriptionId]
    ON [dbo].[Provider]([GatewaySubscriptionId])
    WHERE [GatewaySubscriptionId] IS NOT NULL;
```

**Step 3: Add indexes to User.sql**

Add at the end of the file:

```sql
GO
CREATE NONCLUSTERED INDEX [IX_User_GatewayCustomerId]
    ON [dbo].[User]([GatewayCustomerId])
    WHERE [GatewayCustomerId] IS NOT NULL;

GO
CREATE NONCLUSTERED INDEX [IX_User_GatewaySubscriptionId]
    ON [dbo].[User]([GatewaySubscriptionId])
    WHERE [GatewaySubscriptionId] IS NOT NULL;
```

**Step 4: Commit**

```bash
git add src/Sql/dbo/Tables/Organization.sql \
        src/Sql/dbo/Tables/Provider.sql \
        src/Sql/dbo/Tables/User.sql
git commit -m "feat(db): add gateway lookup indexes to Organization, Provider, and User table definitions"
```

---

### Task 3: Create Migration Script

**Files:**
- Create: `util/Migrator/DbScripts/2026-02-04_00_AddGatewayLookupIndexesAndProcs.sql`

**Step 1: Create the migration script with all changes**

```sql
-- Add indexes for Organization
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Organization_GatewayCustomerId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Organization_GatewayCustomerId]
        ON [dbo].[Organization]([GatewayCustomerId])
        WHERE [GatewayCustomerId] IS NOT NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Organization_GatewaySubscriptionId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Organization_GatewaySubscriptionId]
        ON [dbo].[Organization]([GatewaySubscriptionId])
        WHERE [GatewaySubscriptionId] IS NOT NULL;
END
GO

-- Add indexes for Provider
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Provider_GatewayCustomerId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Provider_GatewayCustomerId]
        ON [dbo].[Provider]([GatewayCustomerId])
        WHERE [GatewayCustomerId] IS NOT NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Provider_GatewaySubscriptionId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Provider_GatewaySubscriptionId]
        ON [dbo].[Provider]([GatewaySubscriptionId])
        WHERE [GatewaySubscriptionId] IS NOT NULL;
END
GO

-- Add indexes for User
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_User_GatewayCustomerId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_User_GatewayCustomerId]
        ON [dbo].[User]([GatewayCustomerId])
        WHERE [GatewayCustomerId] IS NOT NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_User_GatewaySubscriptionId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_User_GatewaySubscriptionId]
        ON [dbo].[User]([GatewaySubscriptionId])
        WHERE [GatewaySubscriptionId] IS NOT NULL;
END
GO

-- Create stored procedures
CREATE OR ALTER PROCEDURE [dbo].[Organization_ReadByGatewayCustomerId]
    @GatewayCustomerId VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationView]
    WHERE
        [GatewayCustomerId] = @GatewayCustomerId
END
GO

CREATE OR ALTER PROCEDURE [dbo].[Organization_ReadByGatewaySubscriptionId]
    @GatewaySubscriptionId VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationView]
    WHERE
        [GatewaySubscriptionId] = @GatewaySubscriptionId
END
GO

CREATE OR ALTER PROCEDURE [dbo].[Provider_ReadByGatewayCustomerId]
    @GatewayCustomerId VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[ProviderView]
    WHERE
        [GatewayCustomerId] = @GatewayCustomerId
END
GO

CREATE OR ALTER PROCEDURE [dbo].[Provider_ReadByGatewaySubscriptionId]
    @GatewaySubscriptionId VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[ProviderView]
    WHERE
        [GatewaySubscriptionId] = @GatewaySubscriptionId
END
GO

CREATE OR ALTER PROCEDURE [dbo].[User_ReadByGatewayCustomerId]
    @GatewayCustomerId VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[UserView]
    WHERE
        [GatewayCustomerId] = @GatewayCustomerId
END
GO

CREATE OR ALTER PROCEDURE [dbo].[User_ReadByGatewaySubscriptionId]
    @GatewaySubscriptionId VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[UserView]
    WHERE
        [GatewaySubscriptionId] = @GatewaySubscriptionId
END
GO
```

**Step 2: Commit**

```bash
git add util/Migrator/DbScripts/2026-02-04_00_AddGatewayLookupIndexesAndProcs.sql
git commit -m "chore(db): add SQL Server migration for gateway lookup indexes and stored procedures"
```

---

### Task 4: Build and Verify SQL Changes

**Step 1: Build the solution to verify no syntax errors**

Run: `dotnet build`

Expected: Build succeeded

**Step 2: Run the SQL Server migration**

Run: `pwsh dev/migrate.ps1 -mssql`

Expected: Migration successful with output showing the new migration script executed:
```
Executing Database Server script 'Bit.Migrator.DbScripts.2026-02-04_00_AddGatewayLookupIndexesAndProcs.sql'
```

**Step 3: Verify stored procedures exist using dbhub MCP**

Use `mcp__dbhub__search_objects` with:
- `object_type`: `procedure`
- `pattern`: `%ReadByGateway%`

Expected: 6 stored procedures found (Organization, Provider, User × CustomerId and SubscriptionId)

**Step 4: Verify indexes exist using dbhub MCP**

Use `mcp__dbhub__search_objects` with:
- `object_type`: `index`
- `pattern`: `%Gateway%`

Expected: 6 new indexes found on Organization, Provider, and User tables

---

## Phase 2: Repository Interface and Dapper Implementation

### Task 5: Add IOrganizationRepository Interface Methods and Dapper Implementation

**Files:**
- Modify: `src/Core/Repositories/IOrganizationRepository.cs`
- Modify: `src/Infrastructure.Dapper/AdminConsole/Repositories/OrganizationRepository.cs`

**Step 1: Add interface method signatures**

Add to `IOrganizationRepository`:

```csharp
Task<Organization?> GetByGatewayCustomerIdAsync(string gatewayCustomerId);
Task<Organization?> GetByGatewaySubscriptionIdAsync(string gatewaySubscriptionId);
```

**Step 2: Implement Dapper methods**

Add to `OrganizationRepository.cs`:

```csharp
public async Task<Organization?> GetByGatewayCustomerIdAsync(string gatewayCustomerId)
{
    using (var connection = new SqlConnection(ConnectionString))
    {
        var results = await connection.QueryAsync<Organization>(
            "[dbo].[Organization_ReadByGatewayCustomerId]",
            new { GatewayCustomerId = gatewayCustomerId },
            commandType: CommandType.StoredProcedure);

        return results.FirstOrDefault();
    }
}

public async Task<Organization?> GetByGatewaySubscriptionIdAsync(string gatewaySubscriptionId)
{
    using (var connection = new SqlConnection(ConnectionString))
    {
        var results = await connection.QueryAsync<Organization>(
            "[dbo].[Organization_ReadByGatewaySubscriptionId]",
            new { GatewaySubscriptionId = gatewaySubscriptionId },
            commandType: CommandType.StoredProcedure);

        return results.FirstOrDefault();
    }
}
```

**Step 3: Build to verify**

Run: `dotnet build src/Infrastructure.Dapper/Infrastructure.Dapper.csproj`

**Step 4: Commit**

```bash
git add src/Core/Repositories/IOrganizationRepository.cs src/Infrastructure.Dapper/AdminConsole/Repositories/OrganizationRepository.cs
git commit -m "feat(repos): add gateway lookup methods to IOrganizationRepository and Dapper implementation"
```

---

### Task 6: Add IProviderRepository Interface Methods and Dapper Implementation

**Files:**
- Modify: `src/Core/Repositories/IProviderRepository.cs`
- Modify: `src/Infrastructure.Dapper/Repositories/ProviderRepository.cs`

**Step 1: Add interface method signatures**

Add to `IProviderRepository`:

```csharp
Task<Provider?> GetByGatewayCustomerIdAsync(string gatewayCustomerId);
Task<Provider?> GetByGatewaySubscriptionIdAsync(string gatewaySubscriptionId);
```

**Step 2: Implement Dapper methods**

Add to Dapper `ProviderRepository.cs`:

```csharp
public async Task<Provider?> GetByGatewayCustomerIdAsync(string gatewayCustomerId)
{
    using (var connection = new SqlConnection(ConnectionString))
    {
        var results = await connection.QueryAsync<Provider>(
            "[dbo].[Provider_ReadByGatewayCustomerId]",
            new { GatewayCustomerId = gatewayCustomerId },
            commandType: CommandType.StoredProcedure);

        return results.FirstOrDefault();
    }
}

public async Task<Provider?> GetByGatewaySubscriptionIdAsync(string gatewaySubscriptionId)
{
    using (var connection = new SqlConnection(ConnectionString))
    {
        var results = await connection.QueryAsync<Provider>(
            "[dbo].[Provider_ReadByGatewaySubscriptionId]",
            new { GatewaySubscriptionId = gatewaySubscriptionId },
            commandType: CommandType.StoredProcedure);

        return results.FirstOrDefault();
    }
}
```

**Step 3: Build to verify**

Run: `dotnet build src/Infrastructure.Dapper/Infrastructure.Dapper.csproj`

**Step 4: Commit**

```bash
git add src/Core/Repositories/IProviderRepository.cs src/Infrastructure.Dapper/Repositories/ProviderRepository.cs
git commit -m "feat(repos): add gateway lookup methods to IProviderRepository and Dapper implementation"
```

---

### Task 7: Add IUserRepository Interface Methods and Dapper Implementation

**Files:**
- Modify: `src/Core/Repositories/IUserRepository.cs`
- Modify: `src/Infrastructure.Dapper/Repositories/UserRepository.cs`

**Step 1: Add interface method signatures**

Add to `IUserRepository`:

```csharp
Task<User?> GetByGatewayCustomerIdAsync(string gatewayCustomerId);
Task<User?> GetByGatewaySubscriptionIdAsync(string gatewaySubscriptionId);
```

**Step 2: Implement Dapper methods**

Add to Dapper `UserRepository.cs`:

```csharp
public async Task<User?> GetByGatewayCustomerIdAsync(string gatewayCustomerId)
{
    using (var connection = new SqlConnection(ConnectionString))
    {
        var results = await connection.QueryAsync<User>(
            "[dbo].[User_ReadByGatewayCustomerId]",
            new { GatewayCustomerId = gatewayCustomerId },
            commandType: CommandType.StoredProcedure);

        return results.FirstOrDefault();
    }
}

public async Task<User?> GetByGatewaySubscriptionIdAsync(string gatewaySubscriptionId)
{
    using (var connection = new SqlConnection(ConnectionString))
    {
        var results = await connection.QueryAsync<User>(
            "[dbo].[User_ReadByGatewaySubscriptionId]",
            new { GatewaySubscriptionId = gatewaySubscriptionId },
            commandType: CommandType.StoredProcedure);

        return results.FirstOrDefault();
    }
}
```

**Step 3: Build to verify**

Run: `dotnet build src/Infrastructure.Dapper/Infrastructure.Dapper.csproj`

**Step 4: Commit**

```bash
git add src/Core/Repositories/IUserRepository.cs src/Infrastructure.Dapper/Repositories/UserRepository.cs
git commit -m "feat(repos): add gateway lookup methods to IUserRepository and Dapper implementation"
```

---

## Phase 3: Entity Framework Implementation

### Task 8: Add EF OrganizationRepository Methods and Index Configuration

**Files:**
- Modify: `src/Infrastructure.EntityFramework/AdminConsole/Repositories/OrganizationRepository.cs`
- Modify: `src/Infrastructure.EntityFramework/AdminConsole/Configurations/OrganizationEntityTypeConfiguration.cs`

**Step 1: Add EF repository methods**

Add to EF `OrganizationRepository.cs`:

```csharp
public async Task<Core.AdminConsole.Entities.Organization?> GetByGatewayCustomerIdAsync(string gatewayCustomerId)
{
    using (var scope = ServiceScopeFactory.CreateScope())
    {
        var dbContext = GetDatabaseContext(scope);
        var organization = await GetDbSet(dbContext)
            .Where(e => e.GatewayCustomerId == gatewayCustomerId)
            .FirstOrDefaultAsync();
        return organization;
    }
}

public async Task<Core.AdminConsole.Entities.Organization?> GetByGatewaySubscriptionIdAsync(string gatewaySubscriptionId)
{
    using (var scope = ServiceScopeFactory.CreateScope())
    {
        var dbContext = GetDatabaseContext(scope);
        var organization = await GetDbSet(dbContext)
            .Where(e => e.GatewaySubscriptionId == gatewaySubscriptionId)
            .FirstOrDefaultAsync();
        return organization;
    }
}
```

**Step 2: Add index configuration**

Add to `OrganizationEntityTypeConfiguration.cs` in the `Configure` method:

```csharp
builder.HasIndex(o => o.GatewayCustomerId);
builder.HasIndex(o => o.GatewaySubscriptionId);
```

**Step 3: Build to verify**

Run: `dotnet build src/Infrastructure.EntityFramework/Infrastructure.EntityFramework.csproj`

**Step 4: Commit**

```bash
git add src/Infrastructure.EntityFramework/AdminConsole/Repositories/OrganizationRepository.cs src/Infrastructure.EntityFramework/AdminConsole/Configurations/OrganizationEntityTypeConfiguration.cs
git commit -m "feat(repos): add EF OrganizationRepository gateway lookup methods and index configuration"
```

---

### Task 9: Add EF ProviderRepository Methods and Index Configuration

**Files:**
- Modify: `src/Infrastructure.EntityFramework/Repositories/ProviderRepository.cs`
- Modify: `src/Infrastructure.EntityFramework/Configurations/ProviderEntityTypeConfiguration.cs` (create if doesn't exist)

**Step 1: Add EF repository methods**

Add to EF `ProviderRepository.cs`:

```csharp
public async Task<Provider?> GetByGatewayCustomerIdAsync(string gatewayCustomerId)
{
    using (var scope = ServiceScopeFactory.CreateScope())
    {
        var dbContext = GetDatabaseContext(scope);
        var provider = await GetDbSet(dbContext)
            .Where(e => e.GatewayCustomerId == gatewayCustomerId)
            .FirstOrDefaultAsync();
        return Mapper.Map<Provider>(provider);
    }
}

public async Task<Provider?> GetByGatewaySubscriptionIdAsync(string gatewaySubscriptionId)
{
    using (var scope = ServiceScopeFactory.CreateScope())
    {
        var dbContext = GetDatabaseContext(scope);
        var provider = await GetDbSet(dbContext)
            .Where(e => e.GatewaySubscriptionId == gatewaySubscriptionId)
            .FirstOrDefaultAsync();
        return Mapper.Map<Provider>(provider);
    }
}
```

**Step 2: Add/update index configuration**

Check if `ProviderEntityTypeConfiguration.cs` exists. If so, add indexes. If not, create it following the Organization pattern.

```csharp
builder.HasIndex(p => p.GatewayCustomerId);
builder.HasIndex(p => p.GatewaySubscriptionId);
```

**Step 3: Build to verify**

Run: `dotnet build src/Infrastructure.EntityFramework/Infrastructure.EntityFramework.csproj`

**Step 4: Commit**

```bash
git add src/Infrastructure.EntityFramework/Repositories/ProviderRepository.cs src/Infrastructure.EntityFramework/Configurations/ProviderEntityTypeConfiguration.cs
git commit -m "feat(repos): add EF ProviderRepository gateway lookup methods and index configuration"
```

---

### Task 10: Add EF UserRepository Methods and Index Configuration

**Files:**
- Modify: `src/Infrastructure.EntityFramework/Repositories/UserRepository.cs`
- Modify: `src/Infrastructure.EntityFramework/Configurations/UserEntityTypeConfiguration.cs` (create if doesn't exist)

**Step 1: Add EF repository methods**

Add to EF `UserRepository.cs`:

```csharp
public async Task<User?> GetByGatewayCustomerIdAsync(string gatewayCustomerId)
{
    using (var scope = ServiceScopeFactory.CreateScope())
    {
        var dbContext = GetDatabaseContext(scope);
        var user = await GetDbSet(dbContext)
            .Where(e => e.GatewayCustomerId == gatewayCustomerId)
            .FirstOrDefaultAsync();
        return Mapper.Map<User>(user);
    }
}

public async Task<User?> GetByGatewaySubscriptionIdAsync(string gatewaySubscriptionId)
{
    using (var scope = ServiceScopeFactory.CreateScope())
    {
        var dbContext = GetDatabaseContext(scope);
        var user = await GetDbSet(dbContext)
            .Where(e => e.GatewaySubscriptionId == gatewaySubscriptionId)
            .FirstOrDefaultAsync();
        return Mapper.Map<User>(user);
    }
}
```

**Step 2: Add/update index configuration**

```csharp
builder.HasIndex(u => u.GatewayCustomerId);
builder.HasIndex(u => u.GatewaySubscriptionId);
```

**Step 3: Build to verify**

Run: `dotnet build src/Infrastructure.EntityFramework/Infrastructure.EntityFramework.csproj`

**Step 4: Commit**

```bash
git add src/Infrastructure.EntityFramework/Repositories/UserRepository.cs src/Infrastructure.EntityFramework/Configurations/UserEntityTypeConfiguration.cs
git commit -m "feat(repos): add EF UserRepository gateway lookup methods and index configuration"
```

---

### Task 11: Generate EF Migrations

**Files:**
- Create: `util/MySqlMigrations/Migrations/*_AddGatewayIndexes.cs`
- Create: `util/PostgresMigrations/Migrations/*_AddGatewayIndexes.cs`
- Create: `util/SqliteMigrations/Migrations/*_AddGatewayIndexes.cs`

**Step 1: Run migration generation script**

Run: `./dev/ef_migrate.ps1 -Name AddGatewayIndexes`

Expected: Migration files created in all three migration projects

**Step 2: Review generated migrations**

Verify each migration adds indexes for:
- `GatewayCustomerId` on Organization, Provider, User
- `GatewaySubscriptionId` on Organization, Provider, User

**Step 3: Build to verify migrations compile**

Run: `dotnet build`

**Step 4: Commit**

```bash
git add util/MySqlMigrations/ util/PostgresMigrations/ util/SqliteMigrations/
git commit -m "chore(db): add EF migrations for gateway lookup indexes"
```

---

## Phase 4: Update SetupIntent Handling

### Task 12: Update SetupIntentSucceededHandler

**Files:**
- Modify: `src/Billing/Services/Implementations/SetupIntentSucceededHandler.cs`
- Modify: `test/Billing.Test/Services/SetupIntentSucceededHandlerTests.cs`

**Step 1: Update handler dependencies**

Replace `ISetupIntentCache` with `IOrganizationRepository` and `IProviderRepository`:

```csharp
public class SetupIntentSucceededHandler(
    IOrganizationRepository organizationRepository,
    IProviderRepository providerRepository,
    IStripeAdapter stripeAdapter,
    // ... other existing dependencies
) : ISetupIntentSucceededHandler
```

**Step 2: Update handler logic**

Replace cache lookup with repository queries:

```csharp
// Old:
var subscriberId = await setupIntentCache.GetSubscriberIdForSetupIntent(setupIntent.Id);
if (subscriberId == null) { return; }
var organization = await organizationRepository.GetByIdAsync(subscriberId.Value);

// New:
var customerId = setupIntent.CustomerId;
if (string.IsNullOrEmpty(customerId))
{
    logger.LogWarning("SetupIntent {SetupIntentId} has no customer ID", setupIntent.Id);
    return;
}

var organization = await organizationRepository.GetByGatewayCustomerIdAsync(customerId);
Provider? provider = null;
if (organization == null)
{
    provider = await providerRepository.GetByGatewayCustomerIdAsync(customerId);
}

if (organization == null && provider == null)
{
    logger.LogError("No organization or provider found for customer {CustomerId}", customerId);
    return;
}
```

**Step 3: Update tests**

Update `SetupIntentSucceededHandlerTests.cs` to:
- Remove `ISetupIntentCache` mock
- Add `IOrganizationRepository` and `IProviderRepository` mocks
- Update test scenarios to use `GetByGatewayCustomerIdAsync`

**Step 4: Run tests**

Run: `dotnet test test/Billing.Test --filter "FullyQualifiedName~SetupIntentSucceededHandler"`

Expected: All tests pass

**Step 5: Commit**

```bash
git add src/Billing/Services/Implementations/SetupIntentSucceededHandler.cs test/Billing.Test/Services/SetupIntentSucceededHandlerTests.cs
git commit -m "refactor(billing): update SetupIntentSucceededHandler to use repository instead of cache"
```

---

### Task 13: Update StripeEventService

**Files:**
- Modify: `src/Billing/Services/Implementations/StripeEventService.cs`
- Modify: `test/Billing.Test/Services/StripeEventServiceTests.cs` (if tests exist)

**Step 1: Update dependencies and logic**

Replace cache usage in `GetCustomerMetadataFromSetupIntentSucceededEvent` with repository queries:

```csharp
// Old:
var subscriberId = await setupIntentCache.GetSubscriberIdForSetupIntent(setupIntent.Id);

// New:
var customerId = setupIntent.CustomerId;
if (string.IsNullOrEmpty(customerId))
{
    return null;
}

var organization = await organizationRepository.GetByGatewayCustomerIdAsync(customerId);
if (organization != null)
{
    // return organization metadata
}

var provider = await providerRepository.GetByGatewayCustomerIdAsync(customerId);
// ...
```

**Step 2: Update tests if they exist**

**Step 3: Run tests**

Run: `dotnet test test/Billing.Test --filter "FullyQualifiedName~StripeEventService"`

**Step 4: Commit**

```bash
git add src/Billing/Services/Implementations/StripeEventService.cs test/Billing.Test/Services/StripeEventServiceTests.cs
git commit -m "refactor(billing): update StripeEventService to use repository instead of cache"
```

---

### Task 14: Update GetPaymentMethodQuery

**Files:**
- Modify: `src/Core/Billing/Payment/Queries/GetPaymentMethodQuery.cs`
- Modify: `test/Core.Test/Billing/Payment/Queries/GetPaymentMethodQueryTests.cs`

**Step 1: Update to query Stripe by customer ID**

Replace:
```csharp
// Old:
var setupIntentId = await setupIntentCache.GetSetupIntentIdForSubscriber(subscriber.Id);
if (!string.IsNullOrEmpty(setupIntentId))
{
    var setupIntent = await stripeAdapter.GetSetupIntentAsync(setupIntentId, ...);
    // ...
}

// New:
if (!string.IsNullOrEmpty(subscriber.GatewayCustomerId))
{
    var setupIntents = await stripeAdapter.ListSetupIntentsAsync(new SetupIntentListOptions
    {
        Customer = subscriber.GatewayCustomerId,
        Expand = new List<string> { "data.payment_method" }
    });

    var unverifiedSetupIntent = setupIntents.FirstOrDefault(si => si.IsUnverifiedBankAccount());
    if (unverifiedSetupIntent != null)
    {
        return MaskedPaymentMethod.From(unverifiedSetupIntent);
    }
}
```

**Step 2: Update tests**

**Step 3: Run tests**

Run: `dotnet test test/Core.Test --filter "FullyQualifiedName~GetPaymentMethodQuery"`

**Step 4: Commit**

```bash
git add src/Core/Billing/Payment/Queries/GetPaymentMethodQuery.cs test/Core.Test/Billing/Payment/Queries/GetPaymentMethodQueryTests.cs
git commit -m "refactor(billing): update GetPaymentMethodQuery to query Stripe by customer ID"
```

---

### Task 15: Update HasPaymentMethodQuery

**Files:**
- Modify: `src/Core/Billing/Payment/Queries/HasPaymentMethodQuery.cs`
- Modify: `test/Core.Test/Billing/Payment/Queries/HasPaymentMethodQueryTests.cs`

**Step 1: Update to query Stripe by customer ID**

Same pattern as GetPaymentMethodQuery.

**Step 2: Update tests**

**Step 3: Run tests**

Run: `dotnet test test/Core.Test --filter "FullyQualifiedName~HasPaymentMethodQuery"`

**Step 4: Commit**

```bash
git add src/Core/Billing/Payment/Queries/HasPaymentMethodQuery.cs test/Core.Test/Billing/Payment/Queries/HasPaymentMethodQueryTests.cs
git commit -m "refactor(billing): update HasPaymentMethodQuery to query Stripe by customer ID"
```

---

## Phase 5: Update SetupIntent Customer Assignment

### Task 16: Update OrganizationBillingService.CreateCustomerAsync

**Files:**
- Modify: `src/Core/Billing/Organizations/Services/OrganizationBillingService.cs`
- Modify: `test/Core.Test/Billing/Organizations/Services/OrganizationBillingServiceTests.cs`

**Step 1: After creating customer, update SetupIntent with customer ID**

After retrieving the SetupIntent and creating the customer, add:

```csharp
// After customer creation, update SetupIntent with customer
await stripeAdapter.UpdateSetupIntentAsync(setupIntent.Id, new SetupIntentUpdateOptions
{
    Customer = customer.Id
});
```

**Step 2: Remove cache.Set() call**

**Step 3: Update tests**

**Step 4: Run tests**

Run: `dotnet test test/Core.Test --filter "FullyQualifiedName~OrganizationBillingService"`

**Step 5: Commit**

```bash
git add src/Core/Billing/Organizations/Services/OrganizationBillingService.cs test/Core.Test/Billing/Organizations/Services/OrganizationBillingServiceTests.cs
git commit -m "refactor(billing): update OrganizationBillingService to set customer on SetupIntent"
```

---

### Task 17: Update ProviderBillingService.SetupCustomer

**Files:**
- Modify: `bitwarden_license/src/Commercial.Core/Billing/Providers/Services/ProviderBillingService.cs`
- Modify: `bitwarden_license/test/Commercial.Core.Test/Billing/Providers/Services/ProviderBillingServiceTests.cs`

**Step 1: After creating customer, update SetupIntent with customer ID**

Same pattern as OrganizationBillingService.

**Step 2: Remove cache.Set() call**

**Step 3: Commit**

```bash
git add bitwarden_license/src/Commercial.Core/Billing/Providers/Services/ProviderBillingService.cs bitwarden_license/test/Commercial.Core.Test/Billing/Providers/Services/ProviderBillingServiceTests.cs
git commit -m "refactor(billing): update ProviderBillingService.SetupCustomer to set customer on SetupIntent"
```

---

### Task 18: Update ProviderBillingService.SetupSubscription

**Files:**
- Modify: `bitwarden_license/src/Commercial.Core/Billing/Providers/Services/ProviderBillingService.cs`
- Modify: `bitwarden_license/test/Commercial.Core.Test/Billing/Providers/Services/ProviderBillingServiceTests.cs`

**Step 1: Replace cache lookup with Stripe query**

Replace:
```csharp
// Old:
var setupIntentId = await setupIntentCache.GetSetupIntentIdForSubscriber(provider.Id);
var setupIntent = !string.IsNullOrEmpty(setupIntentId) ? await stripeAdapter.GetSetupIntentAsync(...) : null;

// New:
SetupIntent? setupIntent = null;
if (!string.IsNullOrEmpty(provider.GatewayCustomerId))
{
    var setupIntents = await stripeAdapter.ListSetupIntentsAsync(new SetupIntentListOptions
    {
        Customer = provider.GatewayCustomerId,
        Expand = new List<string> { "data.payment_method" }
    });
    setupIntent = setupIntents.FirstOrDefault(si => si.IsUnverifiedBankAccount());
}
```

**Step 2: Update tests**

**Step 3: Commit**

```bash
git add bitwarden_license/src/Commercial.Core/Billing/Providers/Services/ProviderBillingService.cs bitwarden_license/test/Commercial.Core.Test/Billing/Providers/Services/ProviderBillingServiceTests.cs
git commit -m "refactor(billing): update ProviderBillingService.SetupSubscription to query Stripe by customer"
```

---

### Task 19: Update UpdatePaymentMethodCommand

**Files:**
- Modify: `src/Core/Billing/Payment/Commands/UpdatePaymentMethodCommand.cs`
- Modify: `test/Core.Test/Billing/Payment/Commands/UpdatePaymentMethodCommandTests.cs`

**Step 1: After getting SetupIntent, update it with customer ID**

```csharp
// After retrieving SetupIntent, update with customer
await stripeAdapter.UpdateSetupIntentAsync(setupIntent.Id, new SetupIntentUpdateOptions
{
    Customer = subscriber.GatewayCustomerId
});
```

**Step 2: Remove cache.Set() call**

**Step 3: Update tests**

**Step 4: Commit**

```bash
git add src/Core/Billing/Payment/Commands/UpdatePaymentMethodCommand.cs test/Core.Test/Billing/Payment/Commands/UpdatePaymentMethodCommandTests.cs
git commit -m "refactor(billing): update UpdatePaymentMethodCommand to set customer on SetupIntent"
```

---

## Phase 6: Remove Dead Code

### Task 20: Remove Bank Account Case from CreatePremiumCloudHostedSubscriptionCommand

**Files:**
- Modify: `src/Core/Billing/Premium/Commands/CreatePremiumCloudHostedSubscriptionCommand.cs`
- Modify: `test/Core.Test/Billing/Premium/Commands/CreatePremiumCloudHostedSubscriptionCommandTests.cs`

**Step 1: Remove PaymentMethodType.BankAccount case from switch**

Remove the entire case block and its Revert logic.

**Step 2: Remove associated tests**

**Step 3: Run tests**

Run: `dotnet test test/Core.Test --filter "FullyQualifiedName~CreatePremiumCloudHostedSubscription"`

**Step 4: Commit**

```bash
git add src/Core/Billing/Premium/Commands/CreatePremiumCloudHostedSubscriptionCommand.cs test/Core.Test/Billing/Premium/Commands/CreatePremiumCloudHostedSubscriptionCommandTests.cs
git commit -m "refactor(billing): remove bank account support from CreatePremiumCloudHostedSubscriptionCommand"
```

---

### Task 21: Remove SubscriberService.UpdatePaymentSource

**Files:**
- Modify: `src/Core/Billing/Services/ISubscriberService.cs`
- Modify: `src/Core/Billing/Services/Implementations/SubscriberService.cs`
- Modify: `test/Core.Test/Billing/Services/SubscriberServiceTests.cs`

**Step 1: Remove interface method**

**Step 2: Remove implementation**

**Step 3: Remove tests**

**Step 4: Build to verify no remaining references**

Run: `dotnet build`

**Step 5: Commit**

```bash
git add src/Core/Billing/Services/ISubscriberService.cs src/Core/Billing/Services/Implementations/SubscriberService.cs test/Core.Test/Billing/Services/SubscriberServiceTests.cs
git commit -m "refactor(billing): remove SubscriberService.UpdatePaymentSource dead code"
```

---

### Task 22: Remove OrganizationBillingService.UpdatePaymentMethod

**Files:**
- Modify: `src/Core/Billing/Organizations/Services/IOrganizationBillingService.cs`
- Modify: `src/Core/Billing/Organizations/Services/OrganizationBillingService.cs`
- Modify: `test/Core.Test/Billing/Organizations/Services/OrganizationBillingServiceTests.cs`

**Step 1: Remove interface method**

**Step 2: Remove implementation**

**Step 3: Remove tests**

**Step 4: Commit**

```bash
git add src/Core/Billing/Organizations/Services/IOrganizationBillingService.cs src/Core/Billing/Organizations/Services/OrganizationBillingService.cs test/Core.Test/Billing/Organizations/Services/OrganizationBillingServiceTests.cs
git commit -m "refactor(billing): remove OrganizationBillingService.UpdatePaymentMethod dead code"
```

---

### Task 23: Remove ProviderBillingService.UpdatePaymentMethod

**Files:**
- Modify: `bitwarden_license/src/Commercial.Core/Billing/Providers/Services/IProviderBillingService.cs`
- Modify: `bitwarden_license/src/Commercial.Core/Billing/Providers/Services/ProviderBillingService.cs`
- Modify: `bitwarden_license/test/Commercial.Core.Test/Billing/Providers/Services/ProviderBillingServiceTests.cs`

**Step 1: Remove interface method**

**Step 2: Remove implementation**

**Step 3: Remove tests**

**Step 4: Commit**

```bash
git add bitwarden_license/src/Commercial.Core/Billing/Providers/Services/IProviderBillingService.cs bitwarden_license/src/Commercial.Core/Billing/Providers/Services/ProviderBillingService.cs bitwarden_license/test/Commercial.Core.Test/Billing/Providers/Services/ProviderBillingServiceTests.cs
git commit -m "refactor(billing): remove ProviderBillingService.UpdatePaymentMethod dead code"
```

---

### Task 24: Remove PremiumUserBillingService Dead Methods

**Files:**
- Modify: `src/Core/Billing/Services/IPremiumUserBillingService.cs`
- Modify: `src/Core/Billing/Services/Implementations/PremiumUserBillingService.cs`
- Modify: `test/Core.Test/Billing/Services/PremiumUserBillingServiceTests.cs`

**Step 1: Remove interface methods (keep only Credit)**

Remove: `Finalize`, `UpdatePaymentMethod`

**Step 2: Remove implementations (keep only Credit)**

Remove: `Finalize`, `UpdatePaymentMethod`, `CreateCustomerAsync`

**Step 3: Remove associated tests**

**Step 4: Commit**

```bash
git add src/Core/Billing/Services/IPremiumUserBillingService.cs src/Core/Billing/Services/Implementations/PremiumUserBillingService.cs test/Core.Test/Billing/Services/PremiumUserBillingServiceTests.cs
git commit -m "refactor(billing): remove PremiumUserBillingService dead methods, keep only Credit"
```

---

### Task 25: Remove UserService Dead Methods

**Files:**
- Modify: `src/Core/Services/IUserService.cs`
- Modify: `src/Core/Services/Implementations/UserService.cs`
- Modify: `test/Core.Test/Services/UserServiceTests.cs`

**Step 1: Remove interface methods**

Remove: `SignUpPremiumAsync`, `ReplacePaymentMethodAsync`

**Step 2: Remove implementations**

**Step 3: Remove associated tests**

**Step 4: Commit**

```bash
git add src/Core/Services/IUserService.cs src/Core/Services/Implementations/UserService.cs test/Core.Test/Services/UserServiceTests.cs
git commit -m "refactor(billing): remove UserService.SignUpPremiumAsync and ReplacePaymentMethodAsync dead code"
```

---

## Phase 7: Remove Cache Infrastructure

### Task 26: Remove ISetupIntentCache and Implementation

**Files:**
- Delete: `src/Core/Billing/Caches/ISetupIntentCache.cs`
- Delete: `src/Core/Billing/Caches/Implementations/SetupIntentDistributedCache.cs`

**Step 1: Delete the files**

```bash
rm src/Core/Billing/Caches/ISetupIntentCache.cs
rm src/Core/Billing/Caches/Implementations/SetupIntentDistributedCache.cs
```

**Step 2: Remove DI registration**

In `src/Core/Billing/Extensions/ServiceCollectionExtensions.cs`, remove:

```csharp
services.AddTransient<ISetupIntentCache, SetupIntentDistributedCache>();
```

**Step 3: Build to verify no remaining references**

Run: `dotnet build`

Expected: Build succeeded (if fails, there are remaining references to fix)

**Step 4: Commit**

```bash
git add -A
git commit -m "refactor(billing): remove ISetupIntentCache and SetupIntentDistributedCache"
```

---

## Phase 8: Final Verification

### Task 27: Run Full Test Suite

**Step 1: Run all tests**

Run: `dotnet test`

Expected: All tests pass

**Step 2: Fix any failing tests**

If tests fail, investigate and fix. Ask questions if stuck.

**Step 3: Build all projects**

Run: `dotnet build`

Expected: Build succeeded with no errors

---

### Task 28: Final Review

**Step 1: Review all changes**

Run: `git log --oneline main..HEAD`

Verify commits are logically organized.

**Step 2: Run git diff against main**

Run: `git diff main --stat`

Verify expected files changed.

---

## Summary of Commits (Expected)

1. `feat(db):` add gateway lookup stored procedures for Organization, Provider, and User
2. `feat(db):` add gateway lookup indexes to Organization, Provider, and User table definitions
3. `chore(db):` add SQL Server migration for gateway lookup indexes and stored procedures
4. `feat(repos):` add gateway lookup methods to IOrganizationRepository and Dapper implementation
5. `feat(repos):` add gateway lookup methods to IProviderRepository and Dapper implementation
6. `feat(repos):` add gateway lookup methods to IUserRepository and Dapper implementation
7. `feat(repos):` add EF OrganizationRepository gateway lookup methods and index configuration
8. `feat(repos):` add EF ProviderRepository gateway lookup methods and index configuration
9. `feat(repos):` add EF UserRepository gateway lookup methods and index configuration
10. `chore(db):` add EF migrations for gateway lookup indexes
11. `refactor(billing):` update SetupIntentSucceededHandler to use repository instead of cache
12. `refactor(billing):` update StripeEventService to use repository instead of cache
13. `refactor(billing):` update GetPaymentMethodQuery to query Stripe by customer ID
14. `refactor(billing):` update HasPaymentMethodQuery to query Stripe by customer ID
15. `refactor(billing):` update OrganizationBillingService to set customer on SetupIntent
16. `refactor(billing):` update ProviderBillingService.SetupCustomer to set customer on SetupIntent
17. `refactor(billing):` update ProviderBillingService.SetupSubscription to query Stripe by customer
18. `refactor(billing):` update UpdatePaymentMethodCommand to set customer on SetupIntent
19. `refactor(billing):` remove bank account support from CreatePremiumCloudHostedSubscriptionCommand
20. `refactor(billing):` remove SubscriberService.UpdatePaymentSource dead code
21. `refactor(billing):` remove OrganizationBillingService.UpdatePaymentMethod dead code
22. `refactor(billing):` remove ProviderBillingService.UpdatePaymentMethod dead code
23. `refactor(billing):` remove PremiumUserBillingService dead methods, keep only Credit
24. `refactor(billing):` remove UserService.SignUpPremiumAsync and ReplacePaymentMethodAsync dead code
25. `refactor(billing):` remove ISetupIntentCache and SetupIntentDistributedCache
