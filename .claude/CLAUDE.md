# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build and Test Commands

```bash
# Build
dotnet build

# Run all tests
dotnet test

# Run a single test project
dotnet test test/Core.Test

# Run a single test class
dotnet test --filter "FullyQualifiedName~DeleteGroupCommandTests"

# Run a single test method
dotnet test --filter "FullyQualifiedName~DeleteGroupCommandTests.DeleteGroup_Success"

# Run locally (API)
dotnet run --project src/Api

# Run locally (Identity)
dotnet run --project src/Identity

# Database migrations
pwsh dev/migrate.ps1              # MSSQL (default)
pwsh dev/migrate.ps1 -postgres    # PostgreSQL
pwsh dev/migrate.ps1 -sqlite      # SQLite
pwsh dev/migrate.ps1 -mysql       # MySQL
pwsh dev/migrate.ps1 -all         # All databases

# Generate OpenAPI specs
pwsh dev/generate_openapi_files.ps1

# Enable git pre-commit hook for formatting
git config --local core.hooksPath .git-hooks
```

## Architecture Overview

### Deployable Services
- **Api** - Main REST API for client applications
- **Identity** - OAuth2/OpenID Connect token server (Duende IdentityServer)
- **Admin** - Administrative portal
- **Billing** - Billing and subscription management
- **Events** - Event logging API
- **EventsProcessor** - Background event processing
- **Notifications** - Real-time push notifications via SignalR
- **Icons** - Domain icon retrieval

### Core Library Structure (`src/Core`)
The Core project is organized by **business domain**, not technical layers:
- `Auth/` - Authentication, SSO, WebAuthn, two-factor
- `Vault/` - Ciphers, folders, collections, sends
- `AdminConsole/` - Organizations, users, policies, groups
- `Billing/` - Subscriptions, payments, licensing
- `SecretsManager/` - Secrets management (commercial)
- `KeyManagement/` - Key rotation, signatures
- `Tools/` - Import/export
- `NotificationCenter/` - User notifications

Cross-cutting concerns:
- `Entities/` - Domain models
- `Repositories/` - Repository interfaces
- `Services/` - Business logic services
- `Context/` - `ICurrentContext` for request-scoped user/org data

### Infrastructure Layer
Two ORM implementations exist for database flexibility:
- **Infrastructure.Dapper** - SQL Server optimized (production cloud)
- **Infrastructure.EntityFramework** - Supports PostgreSQL, MySQL, SQLite (self-hosted)

### Configuration
- `dev/secrets.json` - Local development settings (copy from `secrets.json.example`)
- `appsettings.{Environment}.json` - Environment-specific config
- `GlobalSettings` class - Strongly-typed configuration

## Testing Patterns

Tests use **xUnit** with **NSubstitute** for mocking and **AutoFixture** for test data. All tests must follow the **AAA (Arrange-Act-Assert)** pattern with clear section comments.

### Unit Tests
Unit tests mock dependencies and test isolated business logic:

```csharp
[SutProviderCustomize]
public class DeleteGroupCommandTests
{
    [Theory, BitAutoData]
    public async Task DeleteGroup_Success(SutProvider<DeleteGroupCommand> sutProvider, Group group)
    {
        // Arrange
        sutProvider.GetDependency<IGroupRepository>()
            .GetByIdAsync(group.Id)
            .Returns(group);

        // Act
        await sutProvider.Sut.DeleteGroupAsync(group.OrganizationId, group.Id);

        // Assert
        await sutProvider.GetDependency<IGroupRepository>().Received(1).DeleteAsync(group);
        await sutProvider.GetDependency<IEventService>().Received(1)
            .LogGroupEventAsync(group, EventType.Group_Deleted);
    }
}
```

Key testing utilities:
- `[BitAutoData]` - AutoFixture attribute for generating test data
- `SutProvider<T>` - Helper for creating system-under-test with mocked dependencies
- `[SutProviderCustomize]` - Attribute to enable SutProvider pattern

**SutProvider advanced usage:**
- **Parameter order with inline data**: `[BitAutoData("value")]` inline parameters come before `SutProvider<T>` in the method signature
- **Non-mock dependencies**: Use `new SutProvider<T>().SetDependency<IInterface>(realInstance).Create()` when you need a real implementation (e.g., `FakeLogger`) instead of a mock
- **Interface matching**: SutProvider matches dependencies by the exact interface type in the constructor

### Integration Tests
Integration tests exercise real code paths with actual database operations. **Do not mock** - use real repositories and test helpers to set up data:

```csharp
public class GroupRepositoryTests
{
    [DatabaseTheory, DatabaseData]
    public async Task AddGroupUsersByIdAsync_CreatesGroupUsers(
        IGroupRepository groupRepository,
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository)
    {
        // Arrange
        var user1 = await userRepository.CreateTestUserAsync("user1");
        var user2 = await userRepository.CreateTestUserAsync("user2");
        var org = await organizationRepository.CreateTestOrganizationAsync();
        var orgUser1 = await organizationUserRepository.CreateTestOrganizationUserAsync(org, user1);
        var orgUser2 = await organizationUserRepository.CreateTestOrganizationUserAsync(org, user2);
        var group = await groupRepository.CreateTestGroupAsync(org);

        // Act
        await groupRepository.AddGroupUsersByIdAsync(group.Id, [orgUser1.Id, orgUser2.Id]);

        // Assert
        var actual = await groupRepository.GetManyUserIdsByIdAsync(group.Id);
        Assert.Equal(new[] { orgUser1.Id, orgUser2.Id }.Order(), actual.Order());
    }
}
```

API integration tests use `ApiApplicationFactory` and real HTTP calls:

```csharp
public class OrganizationsControllerTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    [Fact]
    public async Task Put_AsOwner_CanUpdateOrganization()
    {
        // Arrange
        await _loginHelper.LoginAsync(_ownerEmail);
        var updateRequest = new OrganizationUpdateRequestModel
        {
            Name = "Updated Organization Name",
            BillingEmail = "newbilling@example.com"
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/organizations/{_organization.Id}", updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var organizationRepository = _factory.GetService<IOrganizationRepository>();
        var updatedOrg = await organizationRepository.GetByIdAsync(_organization.Id);
        Assert.Equal("Updated Organization Name", updatedOrg.Name);
    }
}
```

Key integration test attributes:
- `[DatabaseTheory, DatabaseData]` - For repository tests against real databases
- `IClassFixture<ApiApplicationFactory>` - For API controller tests

## Critical Rules

- **NEVER** compromise zero-knowledge principles: User vault data must remain encrypted and inaccessible to Bitwarden servers
- **NEVER** log sensitive data: No PII, passwords, keys, or vault data in logs or error messages
- **NEVER** use code regions: Refactor for better readability instead
- **ALWAYS** add unit tests with mocking for new features
- **ALWAYS** use nullable reference types (enabled project-wide)

## Key Patterns

- **TryAdd DI pattern** - Services registered via extension methods (ADR 0026)
- **Authorization handlers** - Custom `IAuthorizationHandler` implementations per domain
- **Feature flags** - `IFeatureService` with LaunchDarkly for opt-in features
- **Tokenables** - `DataProtectorTokenFactory` for secure token generation
- **Self-hosted branching** - `if (globalSettings.SelfHosted)` for deployment-specific logic

## References

- [Server architecture](https://contributing.bitwarden.com/architecture/server/)
- [ADRs](https://contributing.bitwarden.com/architecture/adr/)
- [Setup guide](https://contributing.bitwarden.com/getting-started/server/guide/)
- [Code style](https://contributing.bitwarden.com/contributing/code-style/)
