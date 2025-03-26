using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.PasswordManager;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.SecretsManager;
using Bit.Core.AdminConsole.Shared.Validation;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Models.StaticStore.Plans;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation;

[SutProviderCustomize]
public class SecretsManagerInviteUserValidationTests
{
    [Theory]
    [BitAutoData]
    public void Validate_GivenOrganizationDoesNotHaveSecretsManagerAndNotTryingToAddSecretsManagerUser_ThenTheRequestIsValid(
        Organization organization)
    {
        organization.UseSecretsManager = false;

        var organizationDto = new InviteOrganization(organization, new FreePlan());
        var subscriptionUpdate = new PasswordManagerSubscriptionUpdate(organizationDto, 0, 0);

        var request = new InviteUserOrganizationValidationRequest
        {
            Invites = [new OrganizationUserInvite("test@email.com", "", false)],
            InviteOrganization = organizationDto,
            PerformedBy = Guid.Empty,
            PerformedAt = default,
            OccupiedPmSeats = 0,
            OccupiedSmSeats = 0
        };

        var update = new SecretsManagerSubscriptionUpdate(request, subscriptionUpdate);

        var result = SecretsManagerInviteUserValidation.Validate(update);

        Assert.IsType<Valid<SecretsManagerSubscriptionUpdate>>(result);
    }

    [Theory]
    [BitAutoData]
    public void Validate_GivenOrganizationDoesNotHaveSecretsManagerAndTryingToAddSecretsManagerUser_ThenShouldReturnInvalidMessage(
        Organization organization)
    {
        organization.UseSecretsManager = false;

        var organizationDto = new InviteOrganization(organization, new FreePlan());
        var subscriptionUpdate = new PasswordManagerSubscriptionUpdate(organizationDto, 0, 0);

        var invite = new OrganizationUserInvite(
            email: "email@test.com",
            assignedCollections: [],
            groups: [],
            type: OrganizationUserType.User,
            permissions: new Permissions(),
            externalId: string.Empty,
            accessSecretsManager: true);

        var request = new InviteUserOrganizationValidationRequest
        {
            Invites = [invite],
            InviteOrganization = organizationDto,
            PerformedBy = Guid.Empty,
            PerformedAt = default,
            OccupiedPmSeats = 0,
            OccupiedSmSeats = 0
        };

        var update = new SecretsManagerSubscriptionUpdate(request, subscriptionUpdate);

        var result = SecretsManagerInviteUserValidation.Validate(update);

        Assert.IsType<Invalid<SecretsManagerSubscriptionUpdate>>(result);
        Assert.Equal(OrganizationNoSecretsManagerError.Code, (result as Invalid<SecretsManagerSubscriptionUpdate>)!.ErrorMessageString);
    }

    [Theory]
    [BitAutoData]
    public void Validate_GivenOrganizationHasSecretsManagerWithoutASeatLimit_ThenShouldBeAllowedToAddSecretsManagerUsers(
        Organization organization)
    {
        organization.SmSeats = null;
        organization.UseSecretsManager = true;

        var organizationDto = new InviteOrganization(organization, new FreePlan());
        var subscriptionUpdate = new PasswordManagerSubscriptionUpdate(organizationDto, 0, 0);

        var request = new InviteUserOrganizationValidationRequest
        {
            Invites = [new OrganizationUserInvite(
                email: "email@test.com",
                assignedCollections: [],
                groups: [],
                type: OrganizationUserType.User,
                permissions: new Permissions(),
                externalId: string.Empty,
                accessSecretsManager: true)],
            InviteOrganization = organizationDto,
            PerformedBy = Guid.Empty,
            PerformedAt = default,
            OccupiedPmSeats = 0,
            OccupiedSmSeats = 0
        };

        var update = new SecretsManagerSubscriptionUpdate(request, subscriptionUpdate);

        var result = SecretsManagerInviteUserValidation.Validate(update);

        Assert.IsType<Valid<SecretsManagerSubscriptionUpdate>>(result);
    }

    [Theory]
    [BitAutoData]
    public void Validate_GivenOrganizationPlanDoesNotAllowAdditionalSeats_ThenShouldNotBeAllowedToAddSecretsManagerUsers(
        Organization organization)
    {
        organization.SmSeats = 4;
        organization.MaxAutoscaleSmSeats = 4;
        organization.UseSecretsManager = true;
        organization.PlanType = PlanType.EnterpriseAnnually;

        var organizationDto = new InviteOrganization(organization, new Enterprise2023Plan(isAnnual: true));
        var subscriptionUpdate = new PasswordManagerSubscriptionUpdate(organizationDto, 0, 0);

        var request = new InviteUserOrganizationValidationRequest
        {
            Invites = [new OrganizationUserInvite(
                email: "email@test.com",
                assignedCollections: [],
                groups: [],
                type: OrganizationUserType.User,
                permissions: new Permissions(),
                externalId: string.Empty,
                accessSecretsManager: true)],
            InviteOrganization = organizationDto,
            PerformedBy = Guid.Empty,
            PerformedAt = default,
            OccupiedPmSeats = 0,
            OccupiedSmSeats = 4
        };

        var update = new SecretsManagerSubscriptionUpdate(request, subscriptionUpdate);

        var result = SecretsManagerInviteUserValidation.Validate(update);

        Assert.IsType<Invalid<SecretsManagerSubscriptionUpdate>>(result);
        Assert.Equal(SecretsManagerSeatLimitReachedError.Code, (result as Invalid<SecretsManagerSubscriptionUpdate>)!.ErrorMessageString);
    }

    [Theory]
    [BitAutoData]
    public void Validate_GivenPasswordManagerSeatsAreTheSameAsSecretsManagerSeats_WhenAttemptingToAddASecretManagerSeatOnly_ThenShouldNotBeAllowedToAddSecretsManagerUsers(
        Organization organization)
    {
        organization.Seats = 0;
        organization.SmSeats = 4;
        organization.MaxAutoscaleSmSeats = 5;
        organization.UseSecretsManager = true;
        organization.PlanType = PlanType.EnterpriseAnnually;

        var inviteOrganization = new InviteOrganization(organization, new Enterprise2023Plan(isAnnual: true));
        var subscriptionUpdate = new PasswordManagerSubscriptionUpdate(inviteOrganization, 0, 0);

        var request = new InviteUserOrganizationValidationRequest
        {
            Invites = [new OrganizationUserInvite(
                email: "email@test.com",
                assignedCollections: [],
                groups: [],
                type: OrganizationUserType.User,
                permissions: new Permissions(),
                externalId: string.Empty,
                accessSecretsManager: true)],
            InviteOrganization = inviteOrganization,
            PerformedBy = Guid.Empty,
            PerformedAt = default,
            OccupiedPmSeats = 0,
            OccupiedSmSeats = 4
        };

        var update = new SecretsManagerSubscriptionUpdate(request, subscriptionUpdate);

        var result = SecretsManagerInviteUserValidation.Validate(update);

        Assert.IsType<Invalid<SecretsManagerSubscriptionUpdate>>(result);
        Assert.Equal(SecretsManagerCannotExceedPasswordManagerError.Code, (result as Invalid<SecretsManagerSubscriptionUpdate>)!.ErrorMessageString);
    }
}
