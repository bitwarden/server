using System.Net;
using System.Text.Json;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.Auth.Enums;
using Bit.Core.Billing.Enums;
using Bit.Seeder.Options;
using Bit.Seeder.Scenes;
using Bit.SeederApi.Models.Response;
using Duende.IdentityModel.Client;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.Scenes;

public class OrganizationSsoSceneTests : IClassFixture<InPlaySeederApiApplicationFactory>, IAsyncLifetime
{
    private const string _authority = "https://idp.example/oauth2/default";
    private const string _clientId = "seeder-client-id";
    private const string _clientSecret = "fake-test-client-secret";
    private const string _metadataAddress = "https://idp.example/oauth2/default/.well-known/openid-configuration";

    private readonly HttpClient _client;
    private readonly InPlaySeederApiApplicationFactory _factory;
    private readonly string _playId = Guid.NewGuid().ToString();

    public OrganizationSsoSceneTests(InPlaySeederApiApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        _client.SetBasicAuthentication(_factory.Username, _factory.Password);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        var deleteResponse = await _client.DeleteAsync($"/seed/{_playId}");
        deleteResponse.EnsureSuccessStatusCode();
        _client.Dispose();
    }

    [Fact]
    public async Task SeedAsync_MissingOrganization_ReturnsBadRequest()
    {
        var organizationId = Guid.NewGuid();

        var response = await _client.PostSceneRequestAsync(_playId, nameof(OrganizationSsoScene),
            Sso(organizationId, "missing-org"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains($"Organization {organizationId} not found.", await response.Content.ReadAsStringAsync());
        Assert.False(await _factory.QueryAsync(db => db.SsoConfigs.AnyAsync(c => c.OrganizationId == organizationId)));
    }

    [Fact]
    public async Task SeedAsync_OrganizationAlreadyHasSsoConfig_ReturnsBadRequestWithoutWrites()
    {
        var org = await _client.SeedOrganizationAsync(_playId);
        var first = await _client.PostSceneAsync(_playId, nameof(OrganizationSsoScene),
            Sso(org.OrganizationId, "first"));
        var firstIdentifier = first.GetProperty("identifier").GetString();

        var response = await _client.PostSceneRequestAsync(_playId, nameof(OrganizationSsoScene),
            Sso(org.OrganizationId, "second"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains($"Organization {org.OrganizationId} already has an SSO configuration.",
            await response.Content.ReadAsStringAsync());
        Assert.Equal(1, await _factory.QueryAsync(db => db.SsoConfigs.CountAsync(c => c.OrganizationId == org.OrganizationId)));
        Assert.Equal(firstIdentifier, await _factory.QueryAsync(db => db.Organizations
            .Where(o => o.Id == org.OrganizationId).Select(o => o.Identifier).SingleAsync()));
    }

    [Fact]
    public async Task SeedAsync_MangledIdentifierIsStoredAndReturned()
    {
        var org = await _client.SeedOrganizationAsync(_playId);
        var identifier = $"sso-{Guid.NewGuid():N}";

        var response = await _client.PostSceneRequestAsync(_playId, nameof(OrganizationSsoScene),
            Sso(org.OrganizationId, identifier));
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        var model = await response.Content.ReadFromJsonAsync<SceneResponseModel>();
        Assert.NotNull(model);
        var returned = ((JsonElement)model!.Result!).GetProperty("identifier").GetString()!;

        // Mangling is on only because InPlaySeederApiApplicationFactory enables play-id tracking.
        Assert.NotEqual(identifier, returned);
        Assert.EndsWith(identifier, returned);
        Assert.Equal(returned, model.MangleMap![identifier]);
        Assert.DoesNotContain(_clientSecret, body);
        Assert.Equal(returned, await _factory.QueryAsync(db => db.Organizations
            .Where(o => o.Id == org.OrganizationId).Select(o => o.Identifier).SingleAsync()));
    }

    [Fact]
    public async Task SeedAsync_EnablesUseSso()
    {
        var organizationId = await SeedOrganizationWithoutSsoAsync();
        Assert.False(await QueryUseSsoAsync(organizationId));

        await _client.PostSceneAsync(_playId, nameof(OrganizationSsoScene), Sso(organizationId, "use-sso"));

        Assert.True(await QueryUseSsoAsync(organizationId));
    }

    [Fact]
    public async Task SeedAsync_CreatesEnabledOidcConfig()
    {
        var org = await _client.SeedOrganizationAsync(_playId);

        await _client.PostSceneAsync(_playId, nameof(OrganizationSsoScene), Sso(org.OrganizationId, "oidc-config"));

        var config = await _factory.QueryAsync(db => db.SsoConfigs.SingleAsync(c => c.OrganizationId == org.OrganizationId));
        var data = config.GetData();
        Assert.True(config.Enabled);
        Assert.Equal(SsoType.OpenIdConnect, data.ConfigType);
        Assert.Equal(_authority, data.Authority);
        Assert.Equal(_clientId, data.ClientId);
        Assert.Equal(_clientSecret, data.ClientSecret);
        Assert.Equal(OpenIdConnectRedirectBehavior.RedirectGet, data.RedirectBehavior);
        Assert.True(data.GetClaimsFromUserInfoEndpoint);
        Assert.Equal(MemberDecryptionType.MasterPassword, data.MemberDecryptionType);
    }

    [Fact]
    public async Task SeedAsync_ForwardsNonDefaultOidcFields()
    {
        var org = await _client.SeedOrganizationAsync(_playId);
        var request = Sso(org.OrganizationId, "non-default");
        request.MetadataAddress = _metadataAddress;
        request.RedirectBehavior = OpenIdConnectRedirectBehavior.FormPost;
        request.GetClaimsFromUserInfoEndpoint = false;

        await _client.PostSceneAsync(_playId, nameof(OrganizationSsoScene), request);

        var data = (await _factory.QueryAsync(db => db.SsoConfigs.SingleAsync(c => c.OrganizationId == org.OrganizationId)))
            .GetData();
        Assert.Equal(_metadataAddress, data.MetadataAddress);
        Assert.Equal(OpenIdConnectRedirectBehavior.FormPost, data.RedirectBehavior);
        Assert.False(data.GetClaimsFromUserInfoEndpoint);
    }

    [Fact]
    public async Task SeedAsync_TrustedDeviceEncryption_WritesTdePolicies()
    {
        var org = await _client.SeedOrganizationAsync(_playId);
        var request = Sso(org.OrganizationId, "tde");
        request.MemberDecryptionType = MemberDecryptionType.TrustedDeviceEncryption;

        await _client.PostSceneAsync(_playId, nameof(OrganizationSsoScene), request);

        var config = await _factory.QueryAsync(db => db.SsoConfigs.SingleAsync(c => c.OrganizationId == org.OrganizationId));
        Assert.Equal(MemberDecryptionType.TrustedDeviceEncryption, config.GetData().MemberDecryptionType);
        var policies = await _factory.QueryAsync(db => db.Policies
            .Where(p => p.OrganizationId == org.OrganizationId).ToListAsync());
        Assert.Equal(3, policies.Count);
        Assert.All(policies, p => Assert.True(p.Enabled));
        Assert.Equal(
            new[] { PolicyType.SingleOrg, PolicyType.ResetPassword, PolicyType.RequireSso }.Order(),
            policies.Select(p => p.Type).Order());
        var resetPassword = policies.Single(p => p.Type == PolicyType.ResetPassword);
        Assert.True(resetPassword.GetDataModel<ResetPasswordDataModel>().AutoEnrollEnabled);
    }

    [Fact]
    public async Task SeedAsync_MasterPassword_WritesNoPolicies()
    {
        var org = await _client.SeedOrganizationAsync(_playId);

        await _client.PostSceneAsync(_playId, nameof(OrganizationSsoScene), Sso(org.OrganizationId, "master-password"));

        Assert.False(await _factory.QueryAsync(db => db.Policies.AnyAsync(p => p.OrganizationId == org.OrganizationId)));
    }

    private async Task<Guid> SeedOrganizationWithoutSsoAsync()
    {
        var owner = await _client.SeedUserAsync(_playId);
        var result = await _client.PostSceneAsync(_playId, nameof(SingleOrganizationScene), new SingleOrganizationScene.Request
        {
            OwnerUserId = owner.UserId,
            PlanType = PlanType.EnterpriseAnnually,
            Name = "SSO Org",
            Domain = $"sso-{Guid.NewGuid():N}.example.com",
            Seats = 10,
            Overrides = new OrganizationOverrides { UseSso = false }
        });

        return result.GetProperty("organizationId").GetGuid();
    }

    private Task<bool> QueryUseSsoAsync(Guid organizationId) =>
        _factory.QueryAsync(db => db.Organizations.Where(o => o.Id == organizationId).Select(o => o.UseSso).SingleAsync());

    private static OrganizationSsoScene.Request Sso(Guid organizationId, string identifier) => new()
    {
        OrganizationId = organizationId,
        Identifier = identifier,
        Authority = _authority,
        ClientId = _clientId,
        ClientSecret = _clientSecret,
    };
}
