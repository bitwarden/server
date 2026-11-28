using System.Net;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Enums;
using Bit.Seeder.Options;
using Bit.Seeder.Scenes;
using Duende.IdentityModel.Client;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.Scenes;

public class OrganizationInviteLinkSceneTests : IClassFixture<InPlaySeederApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly InPlaySeederApiApplicationFactory _factory;
    private readonly string _playId = Guid.NewGuid().ToString();

    public OrganizationInviteLinkSceneTests(InPlaySeederApiApplicationFactory factory)
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
    public async Task SeedAsync_EnterpriseOrganization_CreatesLinkAndReturnsWorkingUrl()
    {
        var organization = await _client.SeedOrganizationAsync(_playId);

        var result = await _client.PostSceneAsync(_playId, nameof(OrganizationInviteLinkScene),
            InviteLink(organization, " Example.com ", "", "acme.example"));

        var code = result.GetProperty("code").GetString()!;
        var fragment = result.GetProperty("inviteUrlFragment").GetString()!;
        var url = result.GetProperty("inviteUrl").GetString()!;

        // Read through the repository, as the server does when the link is opened: Code is data-protected at rest.
        OrganizationInviteLink? link;
        using (var scope = _factory.Services.CreateScope())
        {
            link = await scope.ServiceProvider.GetRequiredService<IOrganizationInviteLinkRepository>()
                .GetByOrganizationIdAsync(organization.OrganizationId);
        }
        Assert.NotNull(link);
        Assert.Equal(result.GetProperty("inviteLinkId").GetGuid(), link.Id);
        Assert.Equal(code, link.Code);
        Assert.Equal(["example.com", "acme.example"], link.GetAllowedDomains());
        Assert.False(link.SupportsConfirmation);

        var prefix = $"#/join/{organization.OrganizationId}/{code}?key=";
        Assert.StartsWith(prefix, fragment);
        var secret = fragment[prefix.Length..];
        Assert.Matches("^[A-Za-z0-9_=-]+$", secret);
        Assert.EndsWith("/" + fragment, url);

        // The secret only lives in the URL; the stored sealed invite must not contain it.
        Assert.False(string.IsNullOrEmpty(link.Invite));
        Assert.DoesNotContain(secret, link.Invite);
    }

    [Fact]
    public async Task SeedAsync_OrganizationWithoutInviteLinks_ReturnsBadRequest()
    {
        var owner = await _client.SeedUserAsync(_playId);
        var organizationResult = await _client.PostSceneAsync(_playId, nameof(SingleOrganizationScene),
            new SingleOrganizationScene.Request
            {
                OwnerUserId = owner.UserId,
                PlanType = PlanType.EnterpriseAnnually,
                Name = "No Invite Links Org",
                Domain = $"no-links-{Guid.NewGuid():N}.example",
                Seats = 10,
                Overrides = new OrganizationOverrides { UseInviteLinks = false }
            });
        var organizationId = organizationResult.GetProperty("organizationId").GetGuid();

        var response = await _client.PostSceneRequestAsync(_playId, nameof(OrganizationInviteLinkScene),
            new OrganizationInviteLinkScene.Request
            {
                OrganizationId = organizationId,
                OrganizationKeyB64 = organizationResult.GetProperty("organizationKeyB64").GetString()!,
                AllowedDomains = ["example.com"]
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("cannot use invite links", await response.Content.ReadAsStringAsync());
        Assert.False(await HasInviteLinkAsync(organizationId));
    }

    [Fact]
    public async Task SeedAsync_OrganizationAlreadyHasLink_ReturnsBadRequest()
    {
        var organization = await _client.SeedOrganizationAsync(_playId);
        await _client.PostSceneAsync(_playId, nameof(OrganizationInviteLinkScene),
            InviteLink(organization, "example.com"));

        var response = await _client.PostSceneRequestAsync(_playId, nameof(OrganizationInviteLinkScene),
            InviteLink(organization, "other.example"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("already has an invite link", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task SeedAsync_NoUsableDomains_ReturnsBadRequest()
    {
        var organization = await _client.SeedOrganizationAsync(_playId);

        var response = await _client.PostSceneRequestAsync(_playId, nameof(OrganizationInviteLinkScene),
            InviteLink(organization, " ", ""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("At least one allowed domain is required", await response.Content.ReadAsStringAsync());
        Assert.False(await HasInviteLinkAsync(organization.OrganizationId));
    }

    [Fact]
    public async Task SeedAsync_WrongOrganizationKey_ReturnsBadRequestAndCreatesNoLink()
    {
        var organization = await _client.SeedOrganizationAsync(_playId);
        var otherOrganization = await _client.SeedOrganizationAsync(_playId);

        var response = await _client.PostSceneRequestAsync(_playId, nameof(OrganizationInviteLinkScene),
            new OrganizationInviteLinkScene.Request
            {
                OrganizationId = organization.OrganizationId,
                OrganizationKeyB64 = otherOrganization.OrganizationKeyB64,
                AllowedDomains = ["example.com"]
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(await HasInviteLinkAsync(organization.OrganizationId));
    }

    [Fact]
    public async Task SeedAsync_UnknownOrganization_ReturnsBadRequest()
    {
        var organization = await _client.SeedOrganizationAsync(_playId);

        var response = await _client.PostSceneRequestAsync(_playId, nameof(OrganizationInviteLinkScene),
            new OrganizationInviteLinkScene.Request
            {
                OrganizationId = Guid.NewGuid(),
                OrganizationKeyB64 = organization.OrganizationKeyB64,
                AllowedDomains = ["example.com"]
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("not found", await response.Content.ReadAsStringAsync());
    }

    private static OrganizationInviteLinkScene.Request InviteLink(
        SeededOrganization organization, params string[] allowedDomains) => new()
        {
            OrganizationId = organization.OrganizationId,
            OrganizationKeyB64 = organization.OrganizationKeyB64,
            AllowedDomains = [.. allowedDomains]
        };

    private Task<bool> HasInviteLinkAsync(Guid organizationId) =>
        _factory.QueryAsync(db => db.OrganizationInviteLinks.AnyAsync(l => l.OrganizationId == organizationId));
}
