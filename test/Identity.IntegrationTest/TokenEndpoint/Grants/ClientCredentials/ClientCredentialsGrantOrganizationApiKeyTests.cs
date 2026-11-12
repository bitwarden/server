using System.Text.Json;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using Xunit;
using static Bit.Identity.IntegrationTest.Helpers.TokenEndpointTestHelpers;
using static Bit.Identity.IntegrationTest.Helpers.TokenResponseAssertions;

namespace Bit.Identity.IntegrationTest.TokenEndpoint.Grants.ClientCredentials;

[SutProviderCustomize]
public class ClientCredentialsGrantOrganizationApiKeyTests : IClassFixture<IdentityApplicationFactory>
{
    private readonly IdentityApplicationFactory _factory;

    public ClientCredentialsGrantOrganizationApiKeyTests(IdentityApplicationFactory factory)
    {
        _factory = factory;
        InitializeSharedFactory(_factory);
    }

    [Theory, BitAutoData]
    public async Task TokenEndpoint_GrantTypeClientCredentials_AsOrganization_Success(Organization organization, Bit.Core.Entities.OrganizationApiKey organizationApiKey)
    {
        var orgRepo = _factory.Services.GetRequiredService<IOrganizationRepository>();
        organization.Enabled = true;
        organization.UseApi = true;
        organization = await orgRepo.CreateAsync(organization);
        organizationApiKey.OrganizationId = organization.Id;
        organizationApiKey.Type = OrganizationApiKeyType.Default;

        var orgApiKeyRepo = _factory.Services.GetRequiredService<IOrganizationApiKeyRepository>();
        await orgApiKeyRepo.CreateAsync(organizationApiKey);

        var context = await _factory.Server.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "grant_type", "client_credentials" },
            { "client_id", $"organization.{organization.Id}" },
            { "client_secret", organizationApiKey.ApiKey },
            { "scope", "api.organization" },
        }));

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);

        await AssertDefaultTokenBodyAsync(context, "api.organization");
    }

    [Fact]
    public async Task TokenEndpoint_GrantTypeClientCredentials_AsOrganization_BadOrgId_Fails()
    {
        var context = await _factory.Server.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "grant_type", "client_credentials" },
            { "client_id", "organization.bad_guid_zz&" },
            { "client_secret", "something" },
            { "scope", "api.organization" },
        }));

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);

        var errorBody = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var error = AssertHelper.AssertJsonProperty(errorBody.RootElement, "error", JsonValueKind.String).GetString();
        Assert.Equal("invalid_client", error);
    }

    /// <summary>
    /// This test currently does not test any code that is not covered by other tests but
    /// it shows that we probably have some dead code in <see cref="ClientStore"/>
    /// for installation, organization, and user they split on a <c>'.'</c> but have already checked that at least one
    /// <c>'.'</c> exists in the <c>client_id</c> by checking it with <see cref="string.StartsWith(string)"/>
    /// I believe that idParts.Length > 1 will ALWAYS return true
    /// </summary>
    [Fact]
    public async Task TokenEndpoint_GrantTypeClientCredentials_AsOrganization_NoIdPart_Fails()
    {
        var context = await _factory.Server.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "grant_type", "client_credentials" },
            { "client_id", "organization." },
            { "client_secret", "something" },
            { "scope", "api.organization" },
        }));

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);

        var errorBody = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var error = AssertHelper.AssertJsonProperty(errorBody.RootElement, "error", JsonValueKind.String).GetString();
        Assert.Equal("invalid_client", error);
    }

    [Fact]
    public async Task TokenEndpoint_GrantTypeClientCredentials_AsOrganization_OrgDoesNotExist_Fails()
    {
        var context = await _factory.Server.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "grant_type", "client_credentials" },
            { "client_id", $"organization.{Guid.NewGuid()}" },
            { "client_secret", "something" },
            { "scope", "api.organization" },
        }));

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);

        var errorBody = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var error = AssertHelper.AssertJsonProperty(errorBody.RootElement, "error", JsonValueKind.String).GetString();
        Assert.Equal("invalid_client", error);
    }
}
