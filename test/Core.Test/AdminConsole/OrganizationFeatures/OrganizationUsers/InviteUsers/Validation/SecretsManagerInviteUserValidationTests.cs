using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.Models;
using Bit.Core.Billing.Enums;
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
    public void Validate_GivenOrganizationDoesNotHaveSecretsManager_ThenShouldNotBeAllowedToAddSecretsManagerUsers(
        Organization organization)
    {
        organization.UseSecretsManager = false;

        var organizationDto = OrganizationDto.FromOrganization(organization);
        var subscriptionUpdate = PasswordManagerSubscriptionUpdate.Create(organizationDto, 0, 0);

        var request = new InviteUserOrganizationValidationRequest
        {
            Invites = [new OrganizationUserInviteDto()],
            Organization = organizationDto,
            PerformedBy = Guid.Empty,
            PerformedAt = default,
            OccupiedPmSeats = 0,
            OccupiedSmSeats = 0
        };

        var update = SecretsManagerSubscriptionUpdate.Create(request, subscriptionUpdate);

        var result = SecretsManagerInviteUserValidation.Validate(update);

        Assert.IsType<Invalid<SecretsManagerSubscriptionUpdate>>(result);
        Assert.Equal(InviteUserValidationErrorMessages.OrganizationNoSecretsManager, result.ErrorMessageString);
    }

    [Theory]
    [BitAutoData]
    public void Validate_GivenOrganizationHasSecretsManagerWithoutASeatLimit_ThenShouldBeAllowedToAddSecretsManagerUsers(
        Organization organization)
    {
        organization.SmSeats = null;
        organization.UseSecretsManager = true;

        var organizationDto = OrganizationDto.FromOrganization(organization);
        var subscriptionUpdate = PasswordManagerSubscriptionUpdate.Create(organizationDto, 0, 0);

        var request = new InviteUserOrganizationValidationRequest
        {
            Invites = [new OrganizationUserInviteDto()],
            Organization = organizationDto,
            PerformedBy = Guid.Empty,
            PerformedAt = default,
            OccupiedPmSeats = 0,
            OccupiedSmSeats = 0
        };

        var update = SecretsManagerSubscriptionUpdate.Create(request, subscriptionUpdate);

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

        var organizationDto = OrganizationDto.FromOrganization(organization);
        var subscriptionUpdate = PasswordManagerSubscriptionUpdate.Create(organizationDto, 0, 0);

        var request = new InviteUserOrganizationValidationRequest
        {
            Invites = [OrganizationUserInviteDto.Create("email@test.com", OrganizationUserInvite.Create(["email@test.com"], [], OrganizationUserType.User, new Permissions(), string.Empty, true))],
            Organization = organizationDto,
            PerformedBy = Guid.Empty,
            PerformedAt = default,
            OccupiedPmSeats = 0,
            OccupiedSmSeats = 4
        };

        var update = SecretsManagerSubscriptionUpdate.Create(request, subscriptionUpdate);

        var result = SecretsManagerInviteUserValidation.Validate(update);

        Assert.IsType<Invalid<SecretsManagerSubscriptionUpdate>>(result);
        Assert.Equal(InviteUserValidationErrorMessages.SecretsManagerSeatLimitReached, result.ErrorMessageString);
    }

    [Theory]
    [BitAutoData]
    public void Validate_GivenPasswordManagerSeatsAreTheSameAsSecretsManagerSeats_WhenAttemptingToAddASecretManagerSeatOnly_ThenShouldNotBeAllowedToAddSecretsManagerUsers(
        Organization organization)
    {
        organization.SmSeats = 4;
        organization.MaxAutoscaleSmSeats = 5;
        organization.UseSecretsManager = true;
        organization.PlanType = PlanType.EnterpriseAnnually;

        var organizationDto = OrganizationDto.FromOrganization(organization);
        var subscriptionUpdate = PasswordManagerSubscriptionUpdate.Create(organizationDto, 0, 0);

        var request = new InviteUserOrganizationValidationRequest
        {
            Invites = [OrganizationUserInviteDto.Create("email@test.com", OrganizationUserInvite.Create(["email@test.com"], [], OrganizationUserType.User, new Permissions(), string.Empty, true))],
            Organization = organizationDto,
            PerformedBy = Guid.Empty,
            PerformedAt = default,
            OccupiedPmSeats = 0,
            OccupiedSmSeats = 4
        };

        var update = SecretsManagerSubscriptionUpdate.Create(request, subscriptionUpdate);

        var result = SecretsManagerInviteUserValidation.Validate(update);

        Assert.IsType<Invalid<SecretsManagerSubscriptionUpdate>>(result);
        Assert.Equal(InviteUserValidationErrorMessages.SecretsManagerCannotExceedPasswordManager, result.ErrorMessageString);
    }
}
