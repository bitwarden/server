# Testing Patterns

This document describes testing patterns and infrastructure used in the Bitwarden server codebase.

## Test Framework Stack

- **xUnit** - Test framework
- **NSubstitute** - Mocking library
- **AutoFixture** - Test data generation
- **SutProvider** - Custom helper for creating system-under-test with mocked dependencies

## AAA Pattern

All tests must follow the **Arrange-Act-Assert** pattern with clear section comments:

```csharp
[Theory, BitAutoData]
public async Task MethodName_Scenario_ExpectedResult(SutProvider<MyService> sutProvider, Entity entity)
{
    // Arrange
    sutProvider.GetDependency<IRepository>()
        .GetByIdAsync(entity.Id)
        .Returns(entity);

    // Act
    var result = await sutProvider.Sut.DoSomethingAsync(entity.Id);

    // Assert
    Assert.NotNull(result);
    await sutProvider.GetDependency<IRepository>().Received(1).GetByIdAsync(entity.Id);
}
```

## Unit Tests

Unit tests mock dependencies and test isolated business logic.

### Key Attributes and Utilities

- `[SutProviderCustomize]` - Class-level attribute to enable SutProvider pattern
- `[Theory, BitAutoData]` - Generates test data via AutoFixture
- `SutProvider<T>` - Creates the system-under-test with all dependencies mocked
- `sutProvider.Sut` - The instance being tested
- `sutProvider.GetDependency<TInterface>()` - Access mocked dependencies for setup or verification

### Basic Example

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

### SutProvider Advanced Usage

#### Parameter Order with Inline Data

When using `[BitAutoData("value")]` with inline test data, the inline parameters come **before** `SutProvider<T>` in the method signature:

```csharp
[Theory]
[BitAutoData("password")]
[BitAutoData("webauthn")]
public async Task ValidateAsync_GrantTypes_ShouldWork(
    string grantType,                              // Inline data first
    SutProvider<MyValidator> sutProvider,          // Then SutProvider
    User user)                                     // Then AutoFixture-generated data
{
    // grantType will be "password" or "webauthn"
}
```

#### Non-Mock Dependencies

By default, SutProvider creates NSubstitute mocks for all constructor dependencies. When you need a real implementation instead of a mock (e.g., `FakeLogger` to verify log output), use `SetDependency`:

```csharp
[Theory, BitAutoData]
public async Task SomeTest_ShouldLogWarning(/* no SutProvider param - we create it manually */)
{
    // Arrange
    var fakeLogger = new FakeLogger<MyTests>();
    var sutProvider = new SutProvider<MyService>()
        .SetDependency<ILogger>(fakeLogger)  // Use real FakeLogger instead of mock
        .Create();

    // Act
    await sutProvider.Sut.DoSomething();

    // Assert
    var logs = fakeLogger.Collector.GetSnapshot();
    Assert.Contains(logs, l => l.Level == LogLevel.Warning);
}
```

#### Interface Matching

`SetDependency<TInterface>()` must match the **exact** interface type in the constructor:

```csharp
// Constructor takes: ILogger logger (non-generic)
public MyService(ILogger logger, IRepository repo) { }

// WRONG - ILogger<T> won't match ILogger
sutProvider.SetDependency<ILogger<MyTests>>(fakeLogger)  // Ignored!

// CORRECT - matches the constructor parameter type exactly
sutProvider.SetDependency<ILogger>(fakeLogger)  // Works!
```

If the types don't match, SutProvider silently ignores the `SetDependency` call and creates a mock instead.

## Integration Tests

Integration tests exercise real code paths with actual database operations. **Do not mock** - use real repositories and test helpers to set up data.

### Repository Integration Tests

Use `[DatabaseTheory, DatabaseData]` for tests against real databases:

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

### API Integration Tests

Use `ApiApplicationFactory` for HTTP-level integration tests:

```csharp
public class OrganizationsControllerTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;

    public OrganizationsControllerTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

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

### Key Integration Test Attributes

- `[DatabaseTheory, DatabaseData]` - For repository tests against real databases
- `IClassFixture<ApiApplicationFactory>` - For API controller tests
- `IAsyncLifetime` - For async setup/teardown
