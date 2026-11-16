using System.Net;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Organizations;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bitwarden.Server.Sdk.Features;
using NSubstitute;
using Xunit;

namespace Bit.Api.IntegrationTest.AdminConsole.Controllers;

public class OrganizationUserControllerPutSelfHostedTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Put_GrantingPrivilegedControlsAtSeatLimitOnSelfHost_ReturnsSelfHostError(bool pamBillingSupportEnabled)
    {
        await using var factory = CreateSelfHostedFactory(pamBillingSupportEnabled);
        var client = factory.CreateClient();
        var (organization, ownerEmail) = await CreateOrganizationAtPrivilegedControlsSeatLimitAsync(factory);
        await new LoginHelper(factory, client).LoginAsync(ownerEmail);
        var (_, member) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(factory, organization.Id,
            OrganizationUserType.User);

        var response = await client.PutAsJsonAsync($"organizations/{organization.Id}/users/{member.Id}",
            GrantPrivilegedControlsRequest());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(new CannotAutoscalePrivilegedControlsSeatsOnSelfHostError().Message,
            await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Put_GrantingPrivilegedControlsAtSeatLimitOnSelfHost_DoesNotGrantOrAddSeats(bool pamBillingSupportEnabled)
    {
        await using var factory = CreateSelfHostedFactory(pamBillingSupportEnabled);
        var client = factory.CreateClient();
        var (organization, ownerEmail) = await CreateOrganizationAtPrivilegedControlsSeatLimitAsync(factory);
        await new LoginHelper(factory, client).LoginAsync(ownerEmail);
        var (_, member) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(factory, organization.Id,
            OrganizationUserType.User);

        await client.PutAsJsonAsync($"organizations/{organization.Id}/users/{member.Id}",
            GrantPrivilegedControlsRequest());

        var reloadedMember = await factory.GetService<IOrganizationUserRepository>().GetByIdAsync(member.Id);
        var reloadedOrganization = await factory.GetService<IOrganizationRepository>().GetByIdAsync(organization.Id);
        Assert.Equal((false, 1), (reloadedMember!.AccessPam, reloadedOrganization!.PamSeats));
    }

    private static ApiApplicationFactory CreateSelfHostedFactory(bool pamBillingSupportEnabled)
    {
        var factory = new ApiApplicationFactory();
        factory.UpdateConfiguration("globalSettings:selfHosted", "true");
        factory.UpdateConfiguration("globalSettings:installation:id", "10000000-0000-0000-0000-000000000000");
        factory.SubstituteService<IFeatureService>(featureService =>
            featureService.IsEnabled(FeatureFlagKeys.PM39883_Pam_Billing_Support).Returns(pamBillingSupportEnabled));
        return factory;
    }

    // The owner holds the only Privileged Controls seat, as on a self-hosted organization created from a license.
    private static async Task<(Organization Organization, string OwnerEmail)> CreateOrganizationAtPrivilegedControlsSeatLimitAsync(
        ApiApplicationFactory factory)
    {
        var ownerEmail = $"org-user-put-self-hosted-{Guid.NewGuid()}@bitwarden.com";
        await factory.LoginWithNewAccount(ownerEmail);

        var organization = await factory.GetService<IOrganizationRepository>().CreateAsync(new Organization
        {
            Name = "Self-Hosted Privileged Controls Org",
            BillingEmail = ownerEmail,
            Plan = "Enterprise (Annually)",
            PlanType = PlanType.EnterpriseAnnually,
            Seats = 5,
            Enabled = true,
            UsePam = true,
            PamSeats = 1,
        });

        var owner = await OrganizationTestHelpers.CreateUserAsync(factory, organization.Id, ownerEmail,
            OrganizationUserType.Owner);
        owner.AccessPam = true;
        await factory.GetService<IOrganizationUserRepository>().ReplaceAsync(owner);

        return (organization, ownerEmail);
    }

    private static OrganizationUserUpdateRequestModel GrantPrivilegedControlsRequest() =>
        new()
        {
            Type = OrganizationUserType.User,
            Permissions = new Permissions(),
            Collections = [],
            Groups = [],
            AccessPam = true
        };
}
