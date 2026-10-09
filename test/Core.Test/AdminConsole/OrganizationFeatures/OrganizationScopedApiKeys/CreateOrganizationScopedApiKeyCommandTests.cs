using System.Security.Cryptography;
using System.Text;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationScopedApiKeys;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.IdentityServer;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationScopedApiKeys;

[SutProviderCustomize]
public class CreateOrganizationScopedApiKeyCommandTests
{
    private static readonly DateTime _now = new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);

    [Theory, BitAutoData]
    public async Task CreateAsync_ValidRequest_StoresKeyWithRequestedValues(Organization organization)
    {
        var sutProvider = GetSutProvider(organization);
        var expireAt = _now.AddDays(30);

        var result = await sutProvider.Sut.CreateAsync(Request(organization.Id,
            scopes: [ApiScopes.ApiOrganizationEventsRead, ApiScopes.ApiOrganizationMembersRead],
            expireAt: expireAt));

        Assert.True(result.IsSuccess);
        var apiKey = result.AsSuccess.ApiKey;
        Assert.Equal(organization.Id, apiKey.OrganizationId);
        Assert.Equal("SIEM export", apiKey.Name);
        Assert.Equal([ApiScopes.ApiOrganizationEventsRead, ApiScopes.ApiOrganizationMembersRead], apiKey.GetScopes());
        Assert.Equal(expireAt, apiKey.ExpireAt);
        Assert.Equal(_now, apiKey.CreationDate);
        await sutProvider.GetDependency<IOrganizationScopedApiKeyRepository>().Received(1).CreateAsync(apiKey);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_ValidRequest_StoresOnlyTheHashOfTheReturnedSecret(Organization organization)
    {
        var sutProvider = GetSutProvider(organization);

        var result = await sutProvider.Sut.CreateAsync(Request(organization.Id));

        var created = result.AsSuccess;
        Assert.Equal(30, created.ClientSecret.Length);
        var expectedHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(created.ClientSecret)));
        await sutProvider.GetDependency<IOrganizationScopedApiKeyRepository>().Received(1)
            .CreateAsync(Arg.Is<OrganizationScopedApiKey>(k =>
                k.ClientSecretHash == expectedHash &&
                !k.ClientSecretHash.Contains(created.ClientSecret) &&
                !k.Scopes.Contains(created.ClientSecret) &&
                !k.Name.Contains(created.ClientSecret)));
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_ValidRequest_LogsCreatedEvent(Organization organization)
    {
        var sutProvider = GetSutProvider(organization);

        await sutProvider.Sut.CreateAsync(Request(organization.Id));

        await sutProvider.GetDependency<IEventService>().Received(1)
            .LogOrganizationEventAsync(organization, EventType.Organization_ScopedApiKeyCreated);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_NameWithSurroundingWhitespace_StoresTrimmedName(Organization organization)
    {
        var sutProvider = GetSutProvider(organization);

        var result = await sutProvider.Sut.CreateAsync(Request(organization.Id, name: "  SIEM export  "));

        Assert.Equal("SIEM export", result.AsSuccess.ApiKey.Name);
    }

    [Theory]
    [BitAutoData("")]
    [BitAutoData("   ")]
    [BitAutoData((string?)null)]
    public async Task CreateAsync_EmptyName_ReturnsNameRequired(string? name, Organization organization)
    {
        var sutProvider = GetSutProvider(organization);

        var result = await sutProvider.Sut.CreateAsync(Request(organization.Id, name: name!));

        Assert.IsType<ScopedApiKeyNameRequired>(result.AsError);
        await AssertNotCreatedAsync(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_NameOf200Characters_Succeeds(Organization organization)
    {
        var sutProvider = GetSutProvider(organization);

        var result = await sutProvider.Sut.CreateAsync(Request(organization.Id, name: new string('a', 200)));

        Assert.True(result.IsSuccess);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_NameOver200Characters_ReturnsNameTooLong(Organization organization)
    {
        var sutProvider = GetSutProvider(organization);

        var result = await sutProvider.Sut.CreateAsync(Request(organization.Id, name: new string('a', 201)));

        Assert.IsType<ScopedApiKeyNameTooLong>(result.AsError);
        await AssertNotCreatedAsync(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_NoScopes_ReturnsScopesRequired(Organization organization)
    {
        var sutProvider = GetSutProvider(organization);

        var result = await sutProvider.Sut.CreateAsync(Request(organization.Id, scopes: []));

        Assert.IsType<ScopedApiKeyScopesRequired>(result.AsError);
        await AssertNotCreatedAsync(sutProvider);
    }

    [Theory]
    [BitAutoData(ApiScopes.ApiOrganization)]
    [BitAutoData(ApiScopes.Api)]
    [BitAutoData("api.organization.policies.write")]
    [BitAutoData("API.ORGANIZATION.EVENTS.READ")]
    public async Task CreateAsync_ScopeOutsideCatalog_ReturnsScopesInvalid(string scope, Organization organization)
    {
        var sutProvider = GetSutProvider(organization);

        var result = await sutProvider.Sut.CreateAsync(Request(organization.Id,
            scopes: [ApiScopes.ApiOrganizationEventsRead, scope]));

        Assert.IsType<ScopedApiKeyScopesInvalid>(result.AsError);
        await AssertNotCreatedAsync(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_DuplicateScopes_ReturnsScopesDuplicated(Organization organization)
    {
        var sutProvider = GetSutProvider(organization);

        var result = await sutProvider.Sut.CreateAsync(Request(organization.Id,
            scopes: [ApiScopes.ApiOrganizationEventsRead, ApiScopes.ApiOrganizationEventsRead]));

        Assert.IsType<ScopedApiKeyScopesDuplicated>(result.AsError);
        await AssertNotCreatedAsync(sutProvider);
    }

    [Theory]
    [BitAutoData(0)]
    [BitAutoData(-1)]
    public async Task CreateAsync_ExpirationNotInFuture_ReturnsExpirationNotInFuture(int offsetSeconds,
        Organization organization)
    {
        var sutProvider = GetSutProvider(organization);

        var result = await sutProvider.Sut.CreateAsync(Request(organization.Id,
            expireAt: _now.AddSeconds(offsetSeconds)));

        Assert.IsType<ScopedApiKeyExpirationNotInFuture>(result.AsError);
        await AssertNotCreatedAsync(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_NoExpiration_StoresKeyWithoutExpiration(Organization organization)
    {
        var sutProvider = GetSutProvider(organization);

        var result = await sutProvider.Sut.CreateAsync(Request(organization.Id, expireAt: null));

        Assert.Null(result.AsSuccess.ApiKey.ExpireAt);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_OrganizationWithoutUseApi_ReturnsApiNotAvailable(Organization organization)
    {
        var sutProvider = GetSutProvider(organization, useApi: false);

        var result = await sutProvider.Sut.CreateAsync(Request(organization.Id));

        Assert.IsType<ScopedApiKeyApiNotAvailable>(result.AsError);
        await AssertNotCreatedAsync(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_OrganizationNotFound_ReturnsOrganizationNotFound(Guid organizationId)
    {
        var sutProvider = new SutProvider<CreateOrganizationScopedApiKeyCommand>().WithFakeTimeProvider().Create();

        var result = await sutProvider.Sut.CreateAsync(Request(organizationId));

        Assert.IsType<ScopedApiKeyOrganizationNotFound>(result.AsError);
        await AssertNotCreatedAsync(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_OrganizationHas19Keys_Succeeds(Organization organization)
    {
        var sutProvider = GetSutProvider(organization, existingKeyCount: 19);

        var result = await sutProvider.Sut.CreateAsync(Request(organization.Id));

        Assert.True(result.IsSuccess);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_OrganizationHas20Keys_ReturnsLimitReached(Organization organization)
    {
        var sutProvider = GetSutProvider(organization, existingKeyCount: 20);

        var result = await sutProvider.Sut.CreateAsync(Request(organization.Id));

        Assert.IsType<ScopedApiKeyLimitReached>(result.AsError);
        await AssertNotCreatedAsync(sutProvider);
    }

    private static SutProvider<CreateOrganizationScopedApiKeyCommand> GetSutProvider(Organization organization,
        int existingKeyCount = 0, bool useApi = true)
    {
        organization.UseApi = useApi;
        var sutProvider = new SutProvider<CreateOrganizationScopedApiKeyCommand>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        sutProvider.GetDependency<IOrganizationScopedApiKeyRepository>()
            .GetManyByOrganizationIdAsync(organization.Id)
            .Returns(Enumerable.Range(0, existingKeyCount)
                .Select(_ => new OrganizationScopedApiKey
                {
                    OrganizationId = organization.Id,
                    Name = "Existing key",
                    ClientSecretHash = "hash",
                    Scopes = "[]",
                })
                .ToList());
        return sutProvider;
    }

    private static CreateOrganizationScopedApiKeyRequest Request(Guid organizationId,
        string name = "SIEM export",
        IEnumerable<string>? scopes = null,
        DateTime? expireAt = null) => new()
        {
            OrganizationId = organizationId,
            Name = name,
            Scopes = scopes ?? [ApiScopes.ApiOrganizationEventsRead],
            ExpireAt = expireAt,
        };

    private static async Task AssertNotCreatedAsync(SutProvider<CreateOrganizationScopedApiKeyCommand> sutProvider)
    {
        await sutProvider.GetDependency<IOrganizationScopedApiKeyRepository>().DidNotReceiveWithAnyArgs()
            .CreateAsync(default!);
        await sutProvider.GetDependency<IEventService>().DidNotReceiveWithAnyArgs()
            .LogOrganizationEventAsync(default(Organization)!, default);
    }
}
