using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.Models.Data.Provider;
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

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.InviteLinks;

[SutProviderCustomize]
public class AcceptInviteLinkMembershipValidatorTests
{
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

    [Theory, BitAutoData]
    public async Task ValidateAsync_NewMember_AutoConfirmEnabled_AndMemberOfAnotherOrg_ReturnsError(
        Organization organization, User user,
        SutProvider<AcceptInviteLinkMembershipValidator> sutProvider)
    {
        SetupValidDependencies(sutProvider, organization, user);
        SetOrganizationMemberships(sutProvider, user, MembershipInOtherOrg());

        var result = await sutProvider.Sut.ValidateAsync(
            BuildRequest(organization, user, autoConfirmPolicyEnabled: true));

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
    public async Task ValidateAsync_NewMember_AutoConfirmEnabled_NoOtherOrg_IsValid(
        Organization organization, User user,
        SutProvider<AcceptInviteLinkMembershipValidator> sutProvider)
    {
        SetupValidDependencies(sutProvider, organization, user);

        var result = await sutProvider.Sut.ValidateAsync(
            BuildRequest(organization, user, autoConfirmPolicyEnabled: true));

        Assert.True(result.IsValid);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_NewMember_AutoConfirmNotEnabled_MemberOfAnotherOrg_IsValid(
        Organization organization, User user,
        SutProvider<AcceptInviteLinkMembershipValidator> sutProvider)
    {
        SetupValidDependencies(sutProvider, organization, user);
        SetOrganizationMemberships(sutProvider, user, MembershipInOtherOrg());

        var result = await sutProvider.Sut.ValidateAsync(
            BuildRequest(organization, user, autoConfirmPolicyEnabled: false));

        Assert.True(result.IsValid);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_NewMember_SingleOrgEnabled_AndMemberOfAnotherOrg_ReturnsError(
        Organization organization, User user,
        SutProvider<AcceptInviteLinkMembershipValidator> sutProvider)
    {
        SetupValidDependencies(sutProvider, organization, user);
        EnableTargetPolicy(sutProvider, organization, PolicyType.SingleOrg);
        SetOrganizationMemberships(sutProvider, user, MembershipInOtherOrg());

        var result = await sutProvider.Sut.ValidateAsync(BuildRequest(organization, user));

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
        SetOrganizationMemberships(sutProvider, user, MembershipInOtherOrg());

        var result = await sutProvider.Sut.ValidateAsync(BuildRequest(organization, user));

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

    // Regression guard: a Staged row (SCIM/Directory-Connector provisioned) was previously routed to the
    // requirement framework, which cannot resolve its target-org policies, so these silently passed.

    [Theory]
    [BitAutoData(PolicyType.TwoFactorAuthentication)]
    [BitAutoData(PolicyType.SingleOrg)]
    public async Task ValidateAsync_StagedMembership_TargetPolicyEnabled_ReturnsError(
        PolicyType policyType,
        Organization organization, User user,
        SutProvider<AcceptInviteLinkMembershipValidator> sutProvider)
    {
        SetupValidDependencies(sutProvider, organization, user);
        EnableTargetPolicy(sutProvider, organization, policyType);
        SetOrganizationMemberships(sutProvider, user, MembershipInOtherOrg());
        var staged = Membership(OrganizationUserStatusType.Staged);

        var result = await sutProvider.Sut.ValidateAsync(BuildRequest(organization, user, existing: staged));

        Assert.True(result.IsError);
        Assert.IsType(ExpectedTargetPolicyError(policyType), result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_StagedMembership_AutoConfirmEnabled_ReturnsError(
        Organization organization, User user,
        SutProvider<AcceptInviteLinkMembershipValidator> sutProvider)
    {
        SetupValidDependencies(sutProvider, organization, user);
        SetOrganizationMemberships(sutProvider, user, MembershipInOtherOrg());
        var staged = Membership(OrganizationUserStatusType.Staged);

        var result = await sutProvider.Sut.ValidateAsync(
            BuildRequest(organization, user, existing: staged, autoConfirmPolicyEnabled: true));

        Assert.True(result.IsError);
        Assert.IsType<UserCannotBelongToAnotherOrganization>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_ExistingInvited_UserRole_TargetTwoFactorEnabled_ReturnsError(
        Organization organization, User user,
        SutProvider<AcceptInviteLinkMembershipValidator> sutProvider)
    {
        SetupValidDependencies(sutProvider, organization, user);
        EnableTargetPolicy(sutProvider, organization, PolicyType.TwoFactorAuthentication);
        var invited = Membership(OrganizationUserStatusType.Invited);

        var result = await sutProvider.Sut.ValidateAsync(BuildRequest(organization, user, existing: invited));

        Assert.True(result.IsError);
        Assert.IsType<TwoFactorRequiredForMembership>(result.AsError);
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Admin)]
    public async Task ValidateAsync_ExistingInvited_ElevatedRole_ExemptFromSingleOrgAndTwoFactor(
        OrganizationUserType role,
        Organization organization, User user,
        SutProvider<AcceptInviteLinkMembershipValidator> sutProvider)
    {
        SetupValidDependencies(sutProvider, organization, user);
        EnableTargetPolicy(sutProvider, organization, PolicyType.SingleOrg);
        EnableTargetPolicy(sutProvider, organization, PolicyType.TwoFactorAuthentication);
        SetOrganizationMemberships(sutProvider, user, MembershipInOtherOrg());
        var invited = Membership(OrganizationUserStatusType.Invited, role);

        var result = await sutProvider.Sut.ValidateAsync(BuildRequest(organization, user, existing: invited));

        Assert.True(result.IsValid);
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Admin)]
    public async Task ValidateAsync_ExistingInvited_ElevatedRole_NotExemptFromAutoConfirm(
        OrganizationUserType role,
        Organization organization, User user,
        SutProvider<AcceptInviteLinkMembershipValidator> sutProvider)
    {
        SetupValidDependencies(sutProvider, organization, user);
        SetOrganizationMemberships(sutProvider, user, MembershipInOtherOrg());
        // The free-org admin limit is a no-op here (GetCountByFreeOrganizationAdminUserAsync defaults to 0),
        // so the Auto-Confirm policy check is what runs.
        var invited = Membership(OrganizationUserStatusType.Invited, role);

        var result = await sutProvider.Sut.ValidateAsync(
            BuildRequest(organization, user, existing: invited, autoConfirmPolicyEnabled: true));

        Assert.True(result.IsError);
        Assert.IsType<UserCannotBelongToAnotherOrganization>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_ExistingInvited_CustomRole_EnforcedForSingleOrg(
        Organization organization, User user,
        SutProvider<AcceptInviteLinkMembershipValidator> sutProvider)
    {
        SetupValidDependencies(sutProvider, organization, user);
        EnableTargetPolicy(sutProvider, organization, PolicyType.SingleOrg);
        SetOrganizationMemberships(sutProvider, user, MembershipInOtherOrg());
        var invited = Membership(OrganizationUserStatusType.Invited, OrganizationUserType.Custom);

        var result = await sutProvider.Sut.ValidateAsync(BuildRequest(organization, user, existing: invited));

        Assert.True(result.IsError);
        Assert.IsType<UserIsAMemberOfAnotherOrganization>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_ExistingInvited_CustomRole_EnforcedForTwoFactor(
        Organization organization, User user,
        SutProvider<AcceptInviteLinkMembershipValidator> sutProvider)
    {
        SetupValidDependencies(sutProvider, organization, user);
        EnableTargetPolicy(sutProvider, organization, PolicyType.TwoFactorAuthentication);
        var invited = Membership(OrganizationUserStatusType.Invited, OrganizationUserType.Custom);

        var result = await sutProvider.Sut.ValidateAsync(BuildRequest(organization, user, existing: invited));

        Assert.True(result.IsError);
        Assert.IsType<TwoFactorRequiredForMembership>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_ExistingInvitedAdmin_FreeOrgAdminLimitReached_ReturnsError(
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
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_ProviderUser_NoPolicies_IsValid(
        Organization organization, User user, Bit.Core.AdminConsole.Entities.Provider.ProviderUser providerUser,
        SutProvider<AcceptInviteLinkMembershipValidator> sutProvider)
    {
        SetupValidDependencies(sutProvider, organization, user);
        sutProvider.GetDependency<IProviderUserRepository>().GetManyByUserAsync(user.Id).Returns([providerUser]);

        var result = await sutProvider.Sut.ValidateAsync(BuildRequest(organization, user));

        Assert.True(result.IsValid);
    }

    // A provider user FOR the target org is exempt from that org's Single Org / 2FA (matching PolicyDetails.IsProvider).
    [Theory, BitAutoData]
    public async Task ValidateAsync_ProviderForOrganization_SingleOrgEnabled_AndMemberOfAnotherOrg_IsValid(
        Organization organization, User user,
        SutProvider<AcceptInviteLinkMembershipValidator> sutProvider)
    {
        SetupValidDependencies(sutProvider, organization, user);
        SetProviderForOrganization(sutProvider, user, organization.Id);
        EnableTargetPolicy(sutProvider, organization, PolicyType.SingleOrg);
        SetOrganizationMemberships(sutProvider, user, MembershipInOtherOrg());

        var result = await sutProvider.Sut.ValidateAsync(BuildRequest(organization, user));

        Assert.True(result.IsValid);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_ProviderForOrganization_TwoFactorRequired_IsValid(
        Organization organization, User user,
        SutProvider<AcceptInviteLinkMembershipValidator> sutProvider)
    {
        SetupValidDependencies(sutProvider, organization, user);
        SetProviderForOrganization(sutProvider, user, organization.Id);
        EnableTargetPolicy(sutProvider, organization, PolicyType.TwoFactorAuthentication);

        var result = await sutProvider.Sut.ValidateAsync(BuildRequest(organization, user));

        Assert.True(result.IsValid);
    }

    // Regression: a provider for a DIFFERENT org is NOT exempt from this org's Single Org / 2FA. The prior
    // coarse "member of any provider" check exempted them incorrectly.
    [Theory, BitAutoData]
    public async Task ValidateAsync_ProviderForAnotherOrganization_SingleOrgEnabled_AndMemberOfAnotherOrg_ReturnsError(
        Organization organization, User user,
        SutProvider<AcceptInviteLinkMembershipValidator> sutProvider)
    {
        SetupValidDependencies(sutProvider, organization, user);
        SetProviderForOrganization(sutProvider, user, Guid.NewGuid());
        EnableTargetPolicy(sutProvider, organization, PolicyType.SingleOrg);
        SetOrganizationMemberships(sutProvider, user, MembershipInOtherOrg());

        var result = await sutProvider.Sut.ValidateAsync(BuildRequest(organization, user));

        Assert.True(result.IsError);
        Assert.IsType<UserIsAMemberOfAnotherOrganization>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_ProviderForAnotherOrganization_TwoFactorRequired_ReturnsError(
        Organization organization, User user,
        SutProvider<AcceptInviteLinkMembershipValidator> sutProvider)
    {
        SetupValidDependencies(sutProvider, organization, user);
        SetProviderForOrganization(sutProvider, user, Guid.NewGuid());
        EnableTargetPolicy(sutProvider, organization, PolicyType.TwoFactorAuthentication);

        var result = await sutProvider.Sut.ValidateAsync(BuildRequest(organization, user));

        Assert.True(result.IsError);
        Assert.IsType<TwoFactorRequiredForMembership>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_ProviderUser_AutoConfirmEnabled_ReturnsProviderUsersCannotAcceptInviteLink(
        Organization organization, User user, Bit.Core.AdminConsole.Entities.Provider.ProviderUser providerUser,
        SutProvider<AcceptInviteLinkMembershipValidator> sutProvider)
    {
        SetupValidDependencies(sutProvider, organization, user);
        sutProvider.GetDependency<IProviderUserRepository>().GetManyByUserAsync(user.Id).Returns([providerUser]);

        var result = await sutProvider.Sut.ValidateAsync(
            BuildRequest(organization, user, autoConfirmPolicyEnabled: true));

        Assert.True(result.IsError);
        Assert.IsType<ProviderUsersCannotAcceptInviteLink>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_AutoEnrollEnabled_MissingKey_ReturnsResetPasswordKeyRequired(
        Organization organization, User user,
        SutProvider<AcceptInviteLinkMembershipValidator> sutProvider)
    {
        SetupValidDependencies(sutProvider, organization, user);

        var result = await sutProvider.Sut.ValidateAsync(
            BuildRequest(organization, user, resetPasswordKey: null, accountRecoveryAutoEnroll: true));

        Assert.True(result.IsError);
        Assert.IsType<ResetPasswordKeyRequired>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_AutoEnrollEnabled_ValidKey_IsValid(
        Organization organization, User user,
        SutProvider<AcceptInviteLinkMembershipValidator> sutProvider)
    {
        SetupValidDependencies(sutProvider, organization, user);

        var result = await sutProvider.Sut.ValidateAsync(
            BuildRequest(organization, user, resetPasswordKey: "2.valid-key", accountRecoveryAutoEnroll: true));

        Assert.True(result.IsValid);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_AutoEnrollNotEnabled_MissingKey_IsValid(
        Organization organization, User user,
        SutProvider<AcceptInviteLinkMembershipValidator> sutProvider)
    {
        SetupValidDependencies(sutProvider, organization, user);

        var result = await sutProvider.Sut.ValidateAsync(
            BuildRequest(organization, user, resetPasswordKey: null, accountRecoveryAutoEnroll: false));

        Assert.True(result.IsValid);
    }

    private static AcceptInviteLinkMembershipValidationRequest BuildRequest(
        Organization organization,
        User user,
        IEnumerable<string>? allowedDomains = null,
        OrganizationUser? existing = null,
        string? resetPasswordKey = null,
        bool autoConfirmPolicyEnabled = false,
        bool accountRecoveryAutoEnroll = false)
        => new()
        {
            Organization = organization,
            User = user,
            AllowedDomains = allowedDomains ?? ["example.com"],
            ExistingMembership = existing,
            ResetPasswordKey = resetPasswordKey,
            AutoConfirmPolicyEnabled = autoConfirmPolicyEnabled,
            AccountRecoveryAutoEnroll = accountRecoveryAutoEnroll,
        };

    private static OrganizationUser MembershipInOtherOrg() =>
        new() { OrganizationId = Guid.NewGuid() };

    // A resolved existing membership in the target org: an email invitation (Invited) or SCIM/Directory-Connector
    // provisioned row (Staged). Both have a null UserId until acceptance.
    private static OrganizationUser Membership(
        OrganizationUserStatusType status, OrganizationUserType type = OrganizationUserType.User) =>
        new()
        {
            Status = status,
            Type = type,
            UserId = null,
            Email = "user@example.com",
        };

    private static PolicyDetails PolicyDetailForOtherOrg(PolicyType policyType) =>
        new()
        {
            OrganizationId = Guid.NewGuid(),
            PolicyType = policyType,
            OrganizationUserStatus = OrganizationUserStatusType.Confirmed,
        };

    private static Type ExpectedTargetPolicyError(PolicyType policyType) => policyType switch
    {
        PolicyType.TwoFactorAuthentication => typeof(TwoFactorRequiredForMembership),
        PolicyType.SingleOrg => typeof(UserIsAMemberOfAnotherOrganization),
        _ => throw new ArgumentOutOfRangeException(nameof(policyType)),
    };

    private static void EnableTargetPolicy(
        SutProvider<AcceptInviteLinkMembershipValidator> sutProvider, Organization organization, PolicyType policyType)
        => sutProvider.GetDependency<IPolicyQuery>()
            .RunAsync(organization.Id, policyType)
            .Returns(new PolicyStatus(organization.Id, policyType) { Enabled = true });

    private static void SetOrganizationMemberships(
        SutProvider<AcceptInviteLinkMembershipValidator> sutProvider, User user, params OrganizationUser[] memberships)
        => sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByUserAsync(user.Id)
            .Returns(memberships.ToList());

    // Makes the user a provider user for the given organization (the org-specific signal matching
    // PolicyDetails.IsProvider, used by the Single Org / 2FA exemption).
    private static void SetProviderForOrganization(
        SutProvider<AcceptInviteLinkMembershipValidator> sutProvider, User user, Guid organizationId)
        => sutProvider.GetDependency<IProviderOrganizationRepository>()
            .GetManyByUserAsync(user.Id)
            .Returns([new ProviderOrganizationProviderDetails { OrganizationId = organizationId }]);

    // Valid baseline: verified email, allowed domain, no provider, no policies enabled, no 2FA required,
    // no other organization memberships, empty cross-org requirements.
    private static void SetupValidDependencies(
        SutProvider<AcceptInviteLinkMembershipValidator> sutProvider, Organization organization, User user)
    {
        organization.UsePolicies = true;
        user.EmailVerified = true;
        user.Email = "user@example.com";

        sutProvider.GetDependency<IProviderUserRepository>().GetManyByUserAsync(user.Id).Returns([]);
        sutProvider.GetDependency<IProviderOrganizationRepository>().GetManyByUserAsync(user.Id).Returns([]);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByUserAsync(user.Id)
            .Returns(new List<OrganizationUser>());

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(user.Id)
            .Returns(new AutomaticUserConfirmationPolicyRequirement([]));
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<SingleOrganizationPolicyRequirement>(user.Id)
            .Returns(new SingleOrganizationPolicyRequirement([]));

        // The validator reads only Single Org and 2FA directly; Auto-Confirm and account-recovery states are
        // supplied on the request by the caller.
        foreach (var policyType in new[] { PolicyType.SingleOrg, PolicyType.TwoFactorAuthentication })
        {
            sutProvider.GetDependency<IPolicyQuery>()
                .RunAsync(organization.Id, policyType)
                .Returns(new PolicyStatus(organization.Id, policyType));
        }
    }
}
