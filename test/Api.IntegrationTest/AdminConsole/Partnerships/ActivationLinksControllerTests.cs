using System.Net;
using System.Text.Json;
using Bit.Api.IntegrationTest.Factories;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums.Partnerships;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Xunit;

namespace Bit.Api.IntegrationTest.AdminConsole.Partnerships;

public class ActivationLinksControllerTests : IClassFixture<ApiApplicationFactory>
{
    private const string Path = "/partnerships/v1/activation-links";
    private const string RegisteredOrigin = "https://partner.example.com";

    private readonly ApiApplicationFactory _factory;

    public ActivationLinksControllerTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _factory.UpdateConfiguration("globalSettings:partnerships:externalIdHashKey", "integration-test-hash-key");
        PartnershipTestHelpers.EnablePartnerSponsorships(_factory);
    }

    [Fact]
    public async Task Post_ProvisionedEntitlement_Returns201WithActivationUrl()
    {
        var (client, partnership) = await CreatePartnerClientAsync();
        await CreateEntitlementAsync(partnership, "customer-1", PartnershipEntitlementState.Provisioned);

        var response = await client.PostAsJsonAsync(Path,
            new { externalId = "customer-1", returnUrl = $"{RegisteredOrigin}/done" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        var prefix = $"{_factory.GetService<GlobalSettings>().BaseServiceUri.Vault}/#/partner-activation?token=";
        var activationUrl = body.GetProperty("activationUrl").GetString()!;
        Assert.StartsWith(prefix, activationUrl);
        Assert.True(activationUrl.Length > prefix.Length);
        Assert.True(body.GetProperty("expiresAt").GetDateTime() > DateTime.UtcNow);
    }

    [Fact]
    public async Task Post_UnknownExternalId_Returns404()
    {
        var (client, _) = await CreatePartnerClientAsync();

        var response = await client.PostAsJsonAsync(Path, new { externalId = "unknown" });

        await AssertErrorAsync(response, HttpStatusCode.NotFound, "not_found");
    }

    [Fact]
    public async Task Post_ActiveEntitlement_Returns409AlreadyBound()
    {
        var (client, partnership) = await CreatePartnerClientAsync();
        await CreateEntitlementAsync(partnership, "customer-1", PartnershipEntitlementState.Active, await CreateUserIdAsync());

        var response = await client.PostAsJsonAsync(Path, new { externalId = "customer-1" });

        await AssertErrorAsync(response, HttpStatusCode.Conflict, "already_bound");
    }

    [Fact]
    public async Task Post_ReturnUrlOnUnregisteredOrigin_Returns400()
    {
        var (client, partnership) = await CreatePartnerClientAsync();
        await CreateEntitlementAsync(partnership, "customer-1", PartnershipEntitlementState.Provisioned);

        var response = await client.PostAsJsonAsync(Path,
            new { externalId = "customer-1", returnUrl = "https://attacker.example.net/done" });

        await AssertErrorAsync(response, HttpStatusCode.BadRequest, "invalid_return_url");
    }

    [Fact]
    public async Task Post_AnotherPartnershipsEntitlement_Returns404()
    {
        var (_, otherPartnership) = await CreatePartnerClientAsync();
        await CreateEntitlementAsync(otherPartnership, "shared-customer", PartnershipEntitlementState.Provisioned);
        var (client, _) = await CreatePartnerClientAsync();

        var response = await client.PostAsJsonAsync(Path, new { externalId = "shared-customer" });

        await AssertErrorAsync(response, HttpStatusCode.NotFound, "not_found");
    }

    [Fact]
    public async Task Post_MissingExternalId_Returns400()
    {
        var (client, _) = await CreatePartnerClientAsync();

        var response = await client.PostAsJsonAsync(Path, new { returnUrl = $"{RegisteredOrigin}/done" });

        await AssertErrorAsync(response, HttpStatusCode.BadRequest, "invalid_request");
    }

    private async Task<(HttpClient Client, OrganizationPartnership Partnership)> CreatePartnerClientAsync()
    {
        var (client, partnership) =
            await PartnershipTestHelpers.CreateOrganizationClientAsync(_factory, PartnershipStatus.Active);
        partnership!.SetRegisteredReturnOrigins([RegisteredOrigin]);
        await _factory.GetService<IOrganizationPartnershipRepository>().ReplaceAsync(partnership);
        return (client, partnership);
    }

    private async Task CreateEntitlementAsync(OrganizationPartnership partnership, string externalId,
        PartnershipEntitlementState state, Guid? userId = null)
    {
        var now = DateTime.UtcNow;
        await _factory.GetService<IOrganizationPartnershipEntitlementRepository>().CreateAsync(
            new OrganizationPartnershipEntitlement
            {
                OrganizationPartnershipId = partnership.Id,
                ExternalId = externalId,
                State = state,
                SponsoredPlanType = SponsoredPlanType.Premium,
                UserId = userId,
                LastAppliedEffectiveDate = now,
                CreationDate = now,
                RevisionDate = now,
            });
    }

    private async Task<Guid> CreateUserIdAsync()
    {
        var email = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(email);
        return (await _factory.GetService<IUserRepository>().GetByEmailAsync(email))!.Id;
    }

    private static async Task AssertErrorAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(code, body.GetProperty("code").GetString());
    }
}
