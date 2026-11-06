using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.Enums;
using Bit.Core.Billing.Enums;
using Bit.Seeder.Options;
using Bit.Seeder.Scenes;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.Scenes;

public class OrganizationSsoSceneTests : IClassFixture<InPlaySeederApiApplicationFactory>, IAsyncLifetime
{
    private const string _authority = "https://idp.example/oauth2/default";
    private const string _clientId = "seeder-client-id";
    private const string _clientSecret = "fake-test-client-secret";

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
    public async Task SeedAsync_TrustedDeviceEncryption_UpsertsExistingPolicies()
    {
        var organizationId = await SeedOrganizationWithoutSsoAsync();
        var existingResetPassword = new Policy
        {
            OrganizationId = organizationId,
            Type = PolicyType.ResetPassword,
            Enabled = false,
        };
        existingResetPassword.SetDataModel(new ResetPasswordDataModel { AutoEnrollEnabled = false });
        using (var scope = _factory.Services.CreateScope())
        {
            var policyRepository = scope.ServiceProvider.GetRequiredService<IPolicyRepository>();
            await policyRepository.CreateAsync(new Policy
            {
                OrganizationId = organizationId,
                Type = PolicyType.SingleOrg,
                Enabled = false,
            });
            await policyRepository.CreateAsync(existingResetPassword);
        }
        var request = Sso(organizationId, "tde-upsert");
        request.MemberDecryptionType = MemberDecryptionType.TrustedDeviceEncryption;

        await _client.PostSceneAsync(_playId, nameof(OrganizationSsoScene), request);

        var policies = await _factory.QueryAsync(db => db.Policies
            .Where(p => p.OrganizationId == organizationId).ToListAsync());
        Assert.Equal(3, policies.Count);
        Assert.All(policies, p => Assert.True(p.Enabled));
        var resetPassword = policies.Single(p => p.Type == PolicyType.ResetPassword);
        Assert.Equal(existingResetPassword.Id, resetPassword.Id);
        Assert.True(resetPassword.GetDataModel<ResetPasswordDataModel>().AutoEnrollEnabled);
        Assert.Single(policies, p => p.Type == PolicyType.RequireSso);
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

    private static OrganizationSsoScene.Request Sso(Guid organizationId, string identifier) => new()
    {
        OrganizationId = organizationId,
        Identifier = identifier,
        Authority = _authority,
        ClientId = _clientId,
        ClientSecret = _clientSecret,
    };
}
