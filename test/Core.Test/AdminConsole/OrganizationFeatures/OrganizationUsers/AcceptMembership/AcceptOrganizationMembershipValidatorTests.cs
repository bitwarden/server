using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AcceptMembership;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Enforcement.AutoConfirm;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements.Errors;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;
using static Bit.Core.AdminConsole.Utilities.v2.Validation.ValidationResultHelpers;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers.AcceptMembership;

[SutProviderCustomize]
public class AcceptOrganizationMembershipValidatorTests
{
    [Theory, BitAutoData]
    public async Task ValidateAsync_WithNoRestrictions_ReturnsValid(
        User user,
        Guid organizationId,
        SutProvider<AcceptOrganizationMembershipValidator> sutProvider)
    {
        SetupHappyPath(organizationId, user, sutProvider);

        var request = new AcceptOrganizationMembershipValidationRequest
        {
            OrganizationId = organizationId,
            User = user,
            AllOrganizationMemberships = [],
        };

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsValid);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WithAutoConfirmViolation_ReturnsInvalid(
        User user,
        Guid organizationId,
        OrganizationUser orgUser,
        OrganizationUser otherOrgUser,
        SutProvider<AcceptOrganizationMembershipValidator> sutProvider)
    {
        SetupHappyPath(organizationId, user, sutProvider);

        sutProvider.GetDependency<IAutomaticUserConfirmationPolicyEnforcementHandler>()
            .IsCompliantAsync(
                Arg.Any<AutomaticUserConfirmationPolicyEnforcementRequest>(),
                Arg.Any<AutomaticUserConfirmationPolicyRequirement>())
            .Returns(Invalid(
                new AutomaticUserConfirmationPolicyEnforcementRequest(organizationId, [orgUser, otherOrgUser], user),
                new UserCannotBelongToAnotherOrganization()));

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(user.Id)
            .Returns(new AutomaticUserConfirmationPolicyRequirement(
            [
                new PolicyDetails { OrganizationId = organizationId }
            ]));

        var request = new AcceptOrganizationMembershipValidationRequest
        {
            OrganizationId = organizationId,
            User = user,
            AllOrganizationMemberships = [],
        };

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<UserCannotBelongToAnotherOrganization>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WithProviderUserWhenAutoConfirmEnabled_ReturnsInvalid(
        User user,
        Guid organizationId,
        OrganizationUser orgUser,
        SutProvider<AcceptOrganizationMembershipValidator> sutProvider)
    {
        SetupHappyPath(organizationId, user, sutProvider);

        sutProvider.GetDependency<IAutomaticUserConfirmationPolicyEnforcementHandler>()
            .IsCompliantAsync(
                Arg.Any<AutomaticUserConfirmationPolicyEnforcementRequest>(),
                Arg.Any<AutomaticUserConfirmationPolicyRequirement>())
            .Returns(Invalid(
                new AutomaticUserConfirmationPolicyEnforcementRequest(organizationId, [orgUser], user),
                new ProviderUsersCannotJoin()));

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(user.Id)
            .Returns(new AutomaticUserConfirmationPolicyRequirement(
            [
                new PolicyDetails { OrganizationId = organizationId }
            ]));

        var request = new AcceptOrganizationMembershipValidationRequest
        {
            OrganizationId = organizationId,
            User = user,
            AllOrganizationMemberships = [],
        };

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<ProviderUsersCannotJoin>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WithSingleOrgViolation_ReturnsInvalid(
        User user,
        Guid organizationId,
        OrganizationUser otherOrgUser,
        SutProvider<AcceptOrganizationMembershipValidator> sutProvider)
    {
        SetupHappyPath(organizationId, user, sutProvider);

        otherOrgUser.UserId = user.Id;
        otherOrgUser.Status = OrganizationUserStatusType.Confirmed;

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<SingleOrganizationPolicyRequirement>(user.Id)
            .Returns(new SingleOrganizationPolicyRequirement(
            [
                new PolicyDetails
                {
                    OrganizationId = organizationId,
                    PolicyType = Bit.Core.AdminConsole.Enums.PolicyType.SingleOrg,
                    OrganizationUserStatus = OrganizationUserStatusType.Confirmed
                }
            ]));

        var request = new AcceptOrganizationMembershipValidationRequest
        {
            OrganizationId = organizationId,
            User = user,
            AllOrganizationMemberships = [otherOrgUser],
        };

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<UserIsAMemberOfAnotherOrganization>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WhenAutoConfirmEnabled_SetsAutoConfirmPolicyEnabledTrue(
        User user,
        Guid organizationId,
        SutProvider<AcceptOrganizationMembershipValidator> sutProvider)
    {
        SetupHappyPath(organizationId, user, sutProvider);

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(user.Id)
            .Returns(new AutomaticUserConfirmationPolicyRequirement(
            [
                new PolicyDetails { OrganizationId = organizationId }
            ]));

        var request = new AcceptOrganizationMembershipValidationRequest
        {
            OrganizationId = organizationId,
            User = user,
            AllOrganizationMemberships = [],
        };

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsValid);
        Assert.True(result.Request.AutoConfirmPolicyEnabled);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WhenAutoConfirmNotEnabled_SetsAutoConfirmPolicyEnabledFalse(
        User user,
        Guid organizationId,
        SutProvider<AcceptOrganizationMembershipValidator> sutProvider)
    {
        SetupHappyPath(organizationId, user, sutProvider);

        var request = new AcceptOrganizationMembershipValidationRequest
        {
            OrganizationId = organizationId,
            User = user,
            AllOrganizationMemberships = [],
        };

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsValid);
        Assert.False(result.Request.AutoConfirmPolicyEnabled);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WithOtherOrgSingleOrgPolicy_ReturnsUserIsAMemberOfAnOrganizationThatHasSingleOrgPolicy(
        User user,
        Guid organizationId,
        SutProvider<AcceptOrganizationMembershipValidator> sutProvider)
    {
        SetupHappyPath(organizationId, user, sutProvider);

        // A different org the user belongs to has SingleOrg enabled
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<SingleOrganizationPolicyRequirement>(user.Id)
            .Returns(new SingleOrganizationPolicyRequirement(
            [
                new PolicyDetails
                {
                    OrganizationId = Guid.NewGuid(), // different org, not the target
                    PolicyType = Bit.Core.AdminConsole.Enums.PolicyType.SingleOrg,
                    OrganizationUserStatus = OrganizationUserStatusType.Confirmed
                }
            ]));

        var request = new AcceptOrganizationMembershipValidationRequest
        {
            OrganizationId = organizationId,
            User = user,
            AllOrganizationMemberships = [],
        };

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<UserIsAMemberOfAnOrganizationThatHasSingleOrgPolicy>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WhenTargetMembershipMissing_StubbsItForHandler(
        User user,
        Guid organizationId,
        SutProvider<AcceptOrganizationMembershipValidator> sutProvider)
    {
        SetupHappyPath(organizationId, user, sutProvider);

        // Capture what the handler receives
        AutomaticUserConfirmationPolicyEnforcementRequest? capturedRequest = null;
        sutProvider.GetDependency<IAutomaticUserConfirmationPolicyEnforcementHandler>()
            .IsCompliantAsync(
                Arg.Do<AutomaticUserConfirmationPolicyEnforcementRequest>(r => capturedRequest = r),
                Arg.Any<AutomaticUserConfirmationPolicyRequirement>())
            .Returns(callInfo => Valid(callInfo.Arg<AutomaticUserConfirmationPolicyEnforcementRequest>()));

        var request = new AcceptOrganizationMembershipValidationRequest
        {
            OrganizationId = organizationId,
            User = user,
            AllOrganizationMemberships = [],  // target org not in the list
        };

        await sutProvider.Sut.ValidateAsync(request);

        Assert.NotNull(capturedRequest);
        Assert.Contains(capturedRequest!.AllOrganizationUsers,
            ou => ou.OrganizationId == organizationId);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WhenTargetMembershipAlreadyPresent_DoesNotDuplicate(
        User user,
        Guid organizationId,
        OrganizationUser existingMembership,
        SutProvider<AcceptOrganizationMembershipValidator> sutProvider)
    {
        SetupHappyPath(organizationId, user, sutProvider);

        existingMembership.OrganizationId = organizationId;
        existingMembership.UserId = user.Id;

        // Capture what the handler receives
        AutomaticUserConfirmationPolicyEnforcementRequest? capturedRequest = null;
        sutProvider.GetDependency<IAutomaticUserConfirmationPolicyEnforcementHandler>()
            .IsCompliantAsync(
                Arg.Do<AutomaticUserConfirmationPolicyEnforcementRequest>(r => capturedRequest = r),
                Arg.Any<AutomaticUserConfirmationPolicyRequirement>())
            .Returns(callInfo => Valid(callInfo.Arg<AutomaticUserConfirmationPolicyEnforcementRequest>()));

        var request = new AcceptOrganizationMembershipValidationRequest
        {
            OrganizationId = organizationId,
            User = user,
            AllOrganizationMemberships = [existingMembership],  // target org already present
            ExistingMembership = existingMembership,
        };

        await sutProvider.Sut.ValidateAsync(request);

        Assert.NotNull(capturedRequest);
        Assert.Single(capturedRequest!.AllOrganizationUsers);
    }

    private static void SetupHappyPath(
        Guid organizationId,
        User user,
        SutProvider<AcceptOrganizationMembershipValidator> sutProvider)
    {
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(user.Id)
            .Returns(new AutomaticUserConfirmationPolicyRequirement([]));

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<SingleOrganizationPolicyRequirement>(user.Id)
            .Returns(new SingleOrganizationPolicyRequirement([]));

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<RequireTwoFactorPolicyRequirement>(user.Id)
            .Returns(new RequireTwoFactorPolicyRequirement([]));

        sutProvider.GetDependency<IAutomaticUserConfirmationPolicyEnforcementHandler>()
            .IsCompliantAsync(
                Arg.Any<AutomaticUserConfirmationPolicyEnforcementRequest>(),
                Arg.Any<AutomaticUserConfirmationPolicyRequirement>())
            .Returns(callInfo => Valid(callInfo.Arg<AutomaticUserConfirmationPolicyEnforcementRequest>()));
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WithTwoFactorViolation_ReturnsTwoFactorRequiredForMembership(
        User user,
        Guid organizationId,
        SutProvider<AcceptOrganizationMembershipValidator> sutProvider)
    {
        SetupHappyPath(organizationId, user, sutProvider);

        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(user)
            .Returns(false);

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<RequireTwoFactorPolicyRequirement>(user.Id)
            .Returns(new RequireTwoFactorPolicyRequirement(
            [
                new PolicyDetails
                {
                    OrganizationId = organizationId,
                    OrganizationUserStatus = OrganizationUserStatusType.Confirmed,
                    PolicyType = PolicyType.TwoFactorAuthentication,
                }
            ]));

        var request = new AcceptOrganizationMembershipValidationRequest
        {
            OrganizationId = organizationId,
            User = user,
            AllOrganizationMemberships = [],
        };
        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<TwoFactorRequiredForMembership>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WhenUserHas2FA_OrgRequires2FA_ReturnsValid(
        User user,
        Guid organizationId,
        SutProvider<AcceptOrganizationMembershipValidator> sutProvider)
    {
        SetupHappyPath(organizationId, user, sutProvider);

        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(user)
            .Returns(true);

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<RequireTwoFactorPolicyRequirement>(user.Id)
            .Returns(new RequireTwoFactorPolicyRequirement(
            [
                new PolicyDetails
                {
                    OrganizationId = organizationId,
                    OrganizationUserStatus = OrganizationUserStatusType.Confirmed,
                    PolicyType = PolicyType.TwoFactorAuthentication,
                }
            ]));

        var request = new AcceptOrganizationMembershipValidationRequest
        {
            OrganizationId = organizationId,
            User = user,
            AllOrganizationMemberships = [],
        };
        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsValid);
    }
}
