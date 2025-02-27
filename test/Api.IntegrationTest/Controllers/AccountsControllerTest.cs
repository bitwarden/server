using System.Net.Http.Headers;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Api.Models.Response;
using Bit.Core;
using Bit.Core.Billing.Enums;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Services;
using NSubstitute;
using Xunit;

namespace Bit.Api.IntegrationTest.Controllers;

public class AccountsControllerTest : IClassFixture<ApiApplicationFactory>
{
    private readonly ApiApplicationFactory _factory;

    public AccountsControllerTest(ApiApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task GetPublicKey()
    {
        var tokens = await _factory.LoginWithNewAccount();
        var client = _factory.CreateClient();

        using var message = new HttpRequestMessage(HttpMethod.Get, "/accounts/profile");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Token);
        var response = await client.SendAsync(message);

        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadFromJsonAsync<ProfileResponseModel>();
        Assert.NotNull(content);
        Assert.Equal("integration-test@bitwarden.com", content.Email);
        Assert.Null(content.Name);
        Assert.False(content.EmailVerified);
        Assert.False(content.Premium);
        Assert.False(content.PremiumFromOrganization);
        Assert.Equal("en-US", content.Culture);
        Assert.Null(content.Key);
        Assert.Null(content.PrivateKey);
        Assert.NotNull(content.SecurityStamp);
    }

    private async Task<string> SetupOrganizationManagedAccount()
    {
        _factory.SubstituteService<IFeatureService>(featureService =>
            featureService.IsEnabled(FeatureFlagKeys.AccountDeprovisioning).Returns(true));

        // Create the owner account
        var ownerEmail = $"{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(ownerEmail);

        // Create the organization
        var (_organization, _) = await OrganizationTestHelpers.SignUpAsync(_factory, plan: PlanType.EnterpriseAnnually2023,
            ownerEmail: ownerEmail, passwordManagerSeats: 10, paymentMethod: PaymentMethodType.Card);

        // Create a new organization member
        var (email, orgUser) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory, _organization.Id,
            OrganizationUserType.Custom, new Permissions { AccessReports = true, ManageScim = true });

        // Add a verified domain
        await OrganizationTestHelpers.CreateVerifiedDomainAsync(_factory, _organization.Id, "bitwarden.com");

        return email;
    }
}
