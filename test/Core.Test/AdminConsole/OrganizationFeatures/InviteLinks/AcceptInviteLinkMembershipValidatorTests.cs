using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AcceptMembership;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements.Errors;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Interfaces;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;
using static Bit.Core.AdminConsole.Utilities.v2.Validation.ValidationResultHelpers;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.InviteLinks;

[SutProviderCustomize]
public class AcceptInviteLinkMembershipValidatorTests
{
    // ----- Common eligibility -----

    [Theory, BitAutoData]
    public async Task ValidateAsync_WithUnverifiedEmail_ReturnsEmailNotVerified(
        Organization organization, User user,
        SutProvider<AcceptInviteLinkMembershipValidator> sutProvider)
    {
        SetupValidDependencies(sutProvider, organization, user);
        user.EmailVerified = false;

        var result = await sutProvider.Sut.ValidateAsync(BuildRequest(organization, user));

        Assert.True(result.IsError);
        Assert.IsType<EmailNotVerified>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WithDisallowedEmailDomain_ReturnsEmailDomainNotAllowed(
        Organization organization, User user,
        SutProvider<AcceptInviteLinkMembershipValidator> sutProvider)
    {
        SetupValidDependencies(sutProvider, organization, user);

        var result = await sutProvider.Sut.ValidateAsync(BuildRequest(organization, user, allowedDomains: ["allowed.com"]));

        Assert.True(result.IsError);
        Assert.IsType<EmailDomainNotAllowed>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WithProviderUser_ReturnsProviderUsersCannotAcceptInviteLink(
        Organization organization, User user, Bit.Core.AdminConsole.Entities.Provider.ProviderUser providerUser,
        SutProvider<AcceptInviteLinkMembershipValidator> sutProvider)
    {
        SetupValidDependencies(sutProvider, organization, user);
        sutProvider.GetDependency<IProviderUserRepository>().GetManyByUserAsync(user.Id).Returns([providerUser]);

        var result = await sutProvider.Sut.ValidateAsync(BuildRequest(organization, user));

        Assert.True(result.IsError);
        Assert.IsType<ProviderUsersCannotAcceptInviteLink>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WithRevokedExistingMembership_ReturnsOrganizationAccessRevoked(
        Organization organization, User user, OrganizationUser existingMembership,
        SutProvider<AcceptInviteLinkMembershipValidator> sutProvider)
    {
        SetupValidDependencies(sutProvider, organization, user);
        existingMembership.Status = OrganizationUserStatusType.Revoked;

        var result = await sutProvider.Sut.ValidateAsync(BuildRequest(organization, user, existing: existingMembership));

        Assert.True(result.IsError);
        Assert.IsType<OrganizationAccessRevoked>(result.AsError);
    }

    [Theory]
    [BitAutoData(OrganizationUserStatusType.Accepted)]
    [BitAutoData(OrganizationUserStatusType.Confirmed)]
    public async Task ValidateAsync_WithAlreadyMember_ReturnsAlreadyOrganizationMember(
        OrganizationUserStatusType status,
        Organization organization, User user, OrganizationUser existingMembership,
        SutProvider<AcceptInviteLinkMembershipValidator> sutProvider)
    {
        SetupValidDependencies(sutProvider, organization, user);
        existingMembership.Status = status;

        var result = await sutProvider.Sut.ValidateAsync(BuildRequest(organization, user, existing: existingMembership));

        Assert.True(result.IsError);
        Assert.IsType<AlreadyOrganizationMember>(result.AsError);
    }

    // ----- New member: Automatic User Confirmation -----

    [Theory, BitAutoData]
    public async Task ValidateAsync_NewMember_AutoConfirmEnabled_AndMemberOfAnotherOrg_ReturnsError(
        Organization organization, User user,
        SutProvider<AcceptInviteLinkMembershipValidator> sutProvider)
    {
        SetupValidDependencies(sutProvider, organization, user);
        EnableTargetPolicy(sutProvider, organization, PolicyType.AutomaticUserConfirmation);

        var result = await sutProvider.Sut.ValidateAsync(
            BuildRequest(organization, user, memberships: [MembershipInOtherOrg()]));

        Assert.True(result.IsError);
        Assert.IsType<UserCannotBelongToAnotherOrganization>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_NewMember_AnotherOrgEnforcesAutoConfirm_ReturnsError(
        Organization organization, User user,
        SutProvider<AcceptInviteLinkMembershipValidator> sutProvider)
    {
        SetupValidDependencies(sutProvider, organization, user);
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(user.Id)
            .Returns(new AutomaticUserConfirmationPolicyRequirement(
                [PolicyDetailForOtherOrg(PolicyType.AutomaticUserConfirmation)]));

        var result = await sutProvider.Sut.ValidateAsync(BuildRequest(organization, user));

        Assert.True(result.IsError);
        Assert.IsType<OtherOrganizationDoesNotAllowOtherMembership>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_NewMember_AutoConfirmEnabled_NoOtherOrg_IsValidAndFlagSet(
        Organization organization, User user,
        SutProvider<AcceptInviteLinkMembershipValidator> sutProvider)
    {
        SetupValidDependencies(sutProvider, organization, user);
        EnableTargetPolicy(sutProvider, organization, PolicyType.AutomaticUserConfirmation);

        var result = await sutProvider.Sut.ValidateAsync(BuildRequest(organization, user));

        Assert.True(result.IsValid);
        Assert.True(result.Request.AutoConfirmPolicyEnabled);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_NewMember_AutoConfirmEnabled_ButUsePoliciesDisabled_IsValid(
        Organization organization, User user,
        SutProvider<AcceptInviteLinkMembershipValidator> sutProvider)
    {
        SetupValidDependencies(sutProvider, organization, user);
        EnableTargetPolicy(sutProvider, organization, PolicyType.AutomaticUserConfirmation);
        organization.UsePolicies = false;

        var result = await sutProvider.Sut.ValidateAsync(
            BuildRequest(organization, user, memberships: [MembershipInOtherOrg()]));

        Assert.True(result.IsValid);
        Assert.False(result.Request.AutoConfirmPolicyEnabled);
    }

    // ----- New member: Single Organization -----

    [Theory, BitAutoData]
    public async Task ValidateAsync_NewMember_SingleOrgEnabled_AndMemberOfAnotherOrg_ReturnsError(
        Organization organization, User user,
        SutProvider<AcceptInviteLinkMembershipValidator> sutProvider)
    {
        SetupValidDependencies(sutProvider, organization, user);
        EnableTargetPolicy(sutProvider, organization, PolicyType.SingleOrg);

        var result = await sutProvider.Sut.ValidateAsync(
            BuildRequest(organization, user, memberships: [MembershipInOtherOrg()]));

        Assert.True(result.IsError);
        Assert.IsType<UserIsAMemberOfAnotherOrganization>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_NewMember_AnotherOrgEnforcesSingleOrg_ReturnsError(
        Organization organization, User user,
        SutProvider<AcceptInviteLinkMembershipValidator> sutProvider)
    {
        SetupValidDependencies(sutProvider, organization, user);
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<SingleOrganizationPolicyRequirement>(user.Id)
            .Returns(new SingleOrganizationPolicyRequirement([PolicyDetailForOtherOrg(PolicyType.SingleOrg)]));

        var result = await sutProvider.Sut.ValidateAsync(
            BuildRequest(organization, user, memberships: [MembershipInOtherOrg()]));

        Assert.True(result.IsError);
        Assert.IsType<UserIsAMemberOfAnOrganizationThatHasSingleOrgPolicy>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_NewMember_SingleOrgEnabled_NoOtherOrg_IsValid(
        Organization organization, User user,
        SutProvider<AcceptInviteLinkMembershipValidator> sutProvider)
    {
        SetupValidDependencies(sutProvider, organization, user);
        EnableTargetPolicy(sutProvider, organization, PolicyType.SingleOrg);

        var result = await sutProvider.Sut.ValidateAsync(BuildRequest(organization, user));

        Assert.True(result.IsValid);
    }

    // ----- New member: Two-Factor Authentication -----

    [Theory, BitAutoData]
    public async Task ValidateAsync_NewMember_TwoFactorRequired_UserLacksTwoFactor_ReturnsError(
        Organization organization, User user,
        SutProvider<AcceptInviteLinkMembershipValidator> sutProvider)
    {
        SetupValidDependencies(sutProvider, organization, user);
        EnableTargetPolicy(sutProvider, organization, PolicyType.TwoFactorAuthentication);

        var result = await sutProvider.Sut.ValidateAsync(BuildRequest(organization, user));

        Assert.True(result.IsError);
        Assert.IsType<TwoFactorRequiredForMembership>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_NewMember_TwoFactorRequired_UserHasTwoFactor_IsValid(
        Organization organization, User user,
        SutProvider<AcceptInviteLinkMembershipValidator> sutProvider)
    {
        SetupValidDependencies(sutProvider, organization, user);
        EnableTargetPolicy(sutProvider, organization, PolicyType.TwoFactorAuthentication);
        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>().TwoFactorIsEnabledAsync(user).Returns(true);

        var result = await sutProvider.Sut.ValidateAsync(BuildRequest(organization, user));

        Assert.True(result.IsValid);
    }

    // ----- Account recovery auto-enroll -----

    [Theory, BitAutoData]
    public async Task ValidateAsync_AutoEnrollEnabled_MissingKey_ReturnsResetPasswordKeyRequired(
        Organization organization, User user,
        SutProvider<AcceptInviteLinkMembershipValidator> sutProvider)
    {
        SetupValidDependencies(sutProvider, organization, user);
        EnableAutoEnrollResetPassword(sutProvider, organization);

        var result = await sutProvider.Sut.ValidateAsync(BuildRequest(organization, user, resetPasswordKey: null));

        Assert.True(result.IsError);
        Assert.IsType<ResetPasswordKeyRequired>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_AutoEnrollEnabled_ValidKey_IsValidAndFlagSet(
        Organization organization, User user,
        SutProvider<AcceptInviteLinkMembershipValidator> sutProvider)
    {
        SetupValidDependencies(sutProvider, organization, user);
        EnableAutoEnrollResetPassword(sutProvider, organization);

        var result = await sutProvider.Sut.ValidateAsync(BuildRequest(organization, user, resetPasswordKey: "2.valid-key"));

        Assert.True(result.IsValid);
        Assert.True(result.Request.AccountRecoveryAutoEnroll);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_AutoEnrollEnabled_ButUsePoliciesDisabled_IsValidAndNotEnrolled(
        Organization organization, User user,
        SutProvider<AcceptInviteLinkMembershipValidator> sutProvider)
    {
        SetupValidDependencies(sutProvider, organization, user);
        EnableAutoEnrollResetPassword(sutProvider, organization);
        organization.UsePolicies = false;

        var result = await sutProvider.Sut.ValidateAsync(BuildRequest(organization, user, resetPasswordKey: null));

        Assert.True(result.IsValid);
        Assert.False(result.Request.AccountRecoveryAutoEnroll);
    }

    // ----- Existing member: delegate to shared validator -----

    [Theory, BitAutoData]
    public async Task ValidateAsync_ExistingMember_DelegatesToSharedValidator_AndPropagatesResult(
        Organization organization, User user, OrganizationUser existingMembership,
        SutProvider<AcceptInviteLinkMembershipValidator> sutProvider)
    {
        SetupValidDependencies(sutProvider, organization, user);
        existingMembership.Status = OrganizationUserStatusType.Invited;
        existingMembership.Type = OrganizationUserType.User;

        sutProvider.GetDependency<IAcceptOrganizationMembershipValidator>()
            .ValidateAsync(Arg.Any<AcceptOrganizationMembershipValidationRequest>())
            .Returns(Task.FromResult(Valid(new AcceptOrganizationMembershipValidationResult
            {
                AutoConfirmPolicyEnabled = true
            })));

        var result = await sutProvider.Sut.ValidateAsync(BuildRequest(organization, user, existing: existingMembership));

        Assert.True(result.IsValid);
        Assert.True(result.Request.AutoConfirmPolicyEnabled);
        await sutProvider.GetDependency<IAcceptOrganizationMembershipValidator>()
            .Received(1)
            .ValidateAsync(Arg.Is<AcceptOrganizationMembershipValidationRequest>(r =>
                r.OrganizationId == organization.Id && r.User == user));
        // The target org's policies are resolved by the shared validator's requirement framework, not read directly.
        await sutProvider.GetDependency<IPolicyQuery>()
            .DidNotReceive()
            .RunAsync(organization.Id, PolicyType.AutomaticUserConfirmation);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_ExistingMember_SharedValidatorError_IsPropagated(
        Organization organization, User user, OrganizationUser existingMembership,
        SutProvider<AcceptInviteLinkMembershipValidator> sutProvider)
    {
        SetupValidDependencies(sutProvider, organization, user);
        existingMembership.Status = OrganizationUserStatusType.Invited;
        existingMembership.Type = OrganizationUserType.User;

        sutProvider.GetDependency<IAcceptOrganizationMembershipValidator>()
            .ValidateAsync(Arg.Any<AcceptOrganizationMembershipValidationRequest>())
            .Returns(Task.FromResult(
                Invalid(new AcceptOrganizationMembershipValidationResult(), new TwoFactorRequiredForMembership())));

        var result = await sutProvider.Sut.ValidateAsync(BuildRequest(organization, user, existing: existingMembership));

        Assert.True(result.IsError);
        Assert.IsType<TwoFactorRequiredForMembership>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_ExistingMember_FreeOrgAdminLimitReached_ReturnsError(
        Organization organization, User user, OrganizationUser existingMembership,
        SutProvider<AcceptInviteLinkMembershipValidator> sutProvider)
    {
        SetupValidDependencies(sutProvider, organization, user);
        organization.PlanType = PlanType.Free;
        existingMembership.Status = OrganizationUserStatusType.Invited;
        existingMembership.Type = OrganizationUserType.Admin;
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetCountByFreeOrganizationAdminUserAsync(user.Id)
            .Returns(1);

        var result = await sutProvider.Sut.ValidateAsync(BuildRequest(organization, user, existing: existingMembership));

        Assert.True(result.IsError);
        Assert.IsType<OnlyOneFreeOrganizationAdminAllowed>(result.AsError);
        await sutProvider.GetDependency<IAcceptOrganizationMembershipValidator>()
            .DidNotReceiveWithAnyArgs()
            .ValidateAsync(Arg.Any<AcceptOrganizationMembershipValidationRequest>());
    }

    // ----- Helpers -----

    private static AcceptInviteLinkMembershipValidationRequest BuildRequest(
        Organization organization,
        User user,
        IEnumerable<string>? allowedDomains = null,
        ICollection<OrganizationUser>? memberships = null,
        OrganizationUser? existing = null,
        string? resetPasswordKey = null)
        => new()
        {
            Organization = organization,
            User = user,
            AllowedDomains = allowedDomains ?? ["example.com"],
            AllOrganizationMemberships = memberships ?? new List<OrganizationUser>(),
            ExistingMembership = existing,
            ResetPasswordKey = resetPasswordKey,
        };

    private static OrganizationUser MembershipInOtherOrg() =>
        new() { OrganizationId = Guid.NewGuid() };

    private static PolicyDetails PolicyDetailForOtherOrg(PolicyType policyType) =>
        new()
        {
            OrganizationId = Guid.NewGuid(),
            PolicyType = policyType,
            OrganizationUserStatus = OrganizationUserStatusType.Confirmed,
        };

    private static void EnableTargetPolicy(
        SutProvider<AcceptInviteLinkMembershipValidator> sutProvider, Organization organization, PolicyType policyType)
        => sutProvider.GetDependency<IPolicyQuery>()
            .RunAsync(organization.Id, policyType)
            .Returns(new PolicyStatus(organization.Id, policyType) { Enabled = true });

    private static void EnableAutoEnrollResetPassword(
        SutProvider<AcceptInviteLinkMembershipValidator> sutProvider, Organization organization)
        => sutProvider.GetDependency<IPolicyQuery>()
            .RunAsync(organization.Id, PolicyType.ResetPassword)
            .Returns(new PolicyStatus(organization.Id, PolicyType.ResetPassword)
            {
                Enabled = true,
                Data = "{\"autoEnrollEnabled\": true}"
            });

    // Valid baseline: verified email, allowed domain, no provider, no policies enabled, no 2FA required,
    // empty cross-org requirements.
    private static void SetupValidDependencies(
        SutProvider<AcceptInviteLinkMembershipValidator> sutProvider, Organization organization, User user)
    {
        organization.UsePolicies = true;
        user.EmailVerified = true;
        user.Email = "user@example.com";

        sutProvider.GetDependency<IProviderUserRepository>().GetManyByUserAsync(user.Id).Returns([]);

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(user.Id)
            .Returns(new AutomaticUserConfirmationPolicyRequirement([]));
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<SingleOrganizationPolicyRequirement>(user.Id)
            .Returns(new SingleOrganizationPolicyRequirement([]));

        foreach (var policyType in new[]
                 {
                     PolicyType.AutomaticUserConfirmation, PolicyType.SingleOrg,
                     PolicyType.TwoFactorAuthentication, PolicyType.ResetPassword
                 })
        {
            sutProvider.GetDependency<IPolicyQuery>()
                .RunAsync(organization.Id, policyType)
                .Returns(new PolicyStatus(organization.Id, policyType));
        }
    }
}
