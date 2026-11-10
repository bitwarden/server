using System.Text.Json;
using Bit.Core.Platform.Installations;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using Xunit;
using static Bit.Identity.IntegrationTest.Helpers.TokenEndpointTestHelpers;
using static Bit.Identity.IntegrationTest.Helpers.TokenResponseAssertions;

namespace Bit.Identity.IntegrationTest.Grants.ClientCredentials;

[SutProviderCustomize]
public class ClientCredentialsGrantInstallationTests : IClassFixture<IdentityApplicationFactory>
{
    private readonly IdentityApplicationFactory _factory;

    public ClientCredentialsGrantInstallationTests(IdentityApplicationFactory factory)
    {
        _factory = factory;
        InitializeSharedFactory(_factory);
    }

    [Theory, BitAutoData]
    public async Task TokenEndpoint_GrantTypeClientCredentials_AsInstallation_InstallationExists_Succeeds(Installation installation)
    {
        var installationRepo = _factory.Services.GetRequiredService<IInstallationRepository>();
        installation = await installationRepo.CreateAsync(installation);

        var context = await _factory.Server.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "grant_type", "client_credentials" },
            { "client_id", $"installation.{installation.Id}" },
            { "client_secret", installation.Key },
            { "scope", "api.push" },
        }));

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        await AssertDefaultTokenBodyAsync(context, "api.push", 24 * SecondsInHour);
    }

    [Fact]
    public async Task TokenEndpoint_GrantTypeClientCredentials_AsInstallation_InstallationDoesNotExist_Fails()
    {
        var context = await _factory.Server.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "grant_type", "client_credentials" },
            { "client_id", $"installation.{Guid.NewGuid()}" },
            { "client_secret", "something" },
            { "scope", "api.push" },
        }));

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);

        var errorBody = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var error = AssertHelper.AssertJsonProperty(errorBody.RootElement, "error", JsonValueKind.String).GetString();
        Assert.Equal("invalid_client", error);
    }

    [Fact]
    public async Task TokenEndpoint_GrantTypeClientCredentials_AsInstallation_BadInstallationId_Fails()
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

    /// <inheritdoc cref="ClientCredentialsGrantOrganizationApiKeyTests.TokenEndpoint_GrantTypeClientCredentials_AsOrganization_NoIdPart_Fails"/>
    [Fact]
    public async Task TokenEndpoint_GrantTypeClientCredentials_AsInstallation_NoIdPart_Fails()
    {
        var context = await _factory.Server.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "grant_type", "client_credentials" },
            { "client_id", "installation." },
            { "client_secret", "something" },
            { "scope", "api.push" },
        }));

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);

        var errorBody = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var error = AssertHelper.AssertJsonProperty(errorBody.RootElement, "error", JsonValueKind.String).GetString();
        Assert.Equal("invalid_client", error);
    }
}
