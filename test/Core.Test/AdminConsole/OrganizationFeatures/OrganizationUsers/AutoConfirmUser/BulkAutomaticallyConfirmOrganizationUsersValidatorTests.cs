using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.DeleteClaimedAccount;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Interfaces;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Test.AdminConsole.AutoFixture;
using Bit.Core.Test.AutoFixture.OrganizationFixtures;
using Bit.Core.Test.AutoFixture.OrganizationUserFixtures;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;

[SutProviderCustomize]
public class BulkAutomaticallyConfirmOrganizationUsersValidatorTests
{
    // ─── Helpers ────────────────────────────────────────────────────────────────

    private static AutomaticallyConfirmOrganizationUserValidationRequest BuildRequest(
        OrganizationUser orgUser, Organization organization) =>
        new()
        {
            PerformedBy = Substitute.For<IActingUser>(),
            DefaultUserCollectionName = string.Empty,
            OrganizationUser = orgUser,
            Organization = organization,
            Key = "test-key"
        };

    /// <summary>
    /// Stubs all bulk-data dependencies to pass so individual tests only need to override
    /// the specific condition they are testing.
    /// </summary>
    private static void SetupPassingBulkDataStubs(
        SutProvider<BulkAutomaticallyConfirmOrganizationUsersValidator> sutProvider,
        Organization organization,
        ICollection<OrganizationUser> orgUsers,
        PolicyStatus autoConfirmPolicy)
    {
        var userIds = orgUsers.Select(ou => ou.UserId!.Value).ToList();

        sutProvider.GetDependency<IPolicyQuery>()
            .RunAsync(organization.Id, PolicyType.AutomaticUserConfirmation)
            .Returns(autoConfirmPolicy);

        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(userIds.Select(id => (id, true)));

        // No RequireTwoFactor policy for any user — 2FA check always passes.
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<RequireTwoFactorPolicyRequirement>(Arg.Any<IEnumerable<Guid>>())
            .Returns(userIds.Select(id => (id, new RequireTwoFactorPolicyRequirement([]))));

        // No AutoConfirm policy active for any user — cross-org checks always pass.
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(Arg.Any<IEnumerable<Guid>>())
            .Returns(userIds.Select(id => (id, new AutomaticUserConfirmationPolicyRequirement([]))));

        // Each user belongs only to the target org.
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByManyUsersAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(orgUsers);

        // No provider memberships.
        sutProvider.GetDependency<IProviderUserRepository>()
            .GetManyByManyUsersAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<ProviderUser>());
    }

    // ─── Empty input ─────────────────────────────────────────────────────────────

    [Theory, BitAutoData]
    public async Task ValidateManyAsync_EmptyInput_ReturnsEmpty(
        SutProvider<BulkAutomaticallyConfirmOrganizationUsersValidator> sutProvider)
    {
        var results = await sutProvider.Sut.ValidateManyAsync([]);

        Assert.Empty(results);
    }

    // ─── Structural failures (no DB calls needed) ────────────────────────────────

    [Theory, BitAutoData]
    public async Task ValidateManyAsync_NullOrganizationUser_ReturnsUserNotFoundError(
        SutProvider<BulkAutomaticallyConfirmOrganizationUsersValidator> sutProvider,
        Organization organization)
    {
        var request = new AutomaticallyConfirmOrganizationUserValidationRequest
        {
            PerformedBy = Substitute.For<IActingUser>(),
            DefaultUserCollectionName = string.Empty,
            OrganizationUser = null,
            OrganizationUserId = Guid.NewGuid(),
            Organization = organization,
            OrganizationId = organization.Id,
            Key = "test-key"
        };

        var results = (await sutProvider.Sut.ValidateManyAsync([request])).ToList();

        Assert.Single(results);
        Assert.True(results[0].IsError);
        Assert.IsType<UserNotFoundError>(results[0].AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateManyAsync_NullUserId_ReturnsUserNotFoundError(
        SutProvider<BulkAutomaticallyConfirmOrganizationUsersValidator> sutProvider,
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser)
    {
        orgUser.UserId = null;
        orgUser.OrganizationId = organization.Id;

        var results = (await sutProvider.Sut.ValidateManyAsync([BuildRequest(orgUser, organization)])).ToList();

        Assert.Single(results);
        Assert.True(results[0].IsError);
        Assert.IsType<UserNotFoundError>(results[0].AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateManyAsync_MismatchedOrganizationId_ReturnsOrganizationUserIdIsInvalidError(
        SutProvider<BulkAutomaticallyConfirmOrganizationUsersValidator> sutProvider,
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser,
        Guid userId)
    {
        orgUser.UserId = userId;
        orgUser.OrganizationId = Guid.NewGuid(); // Different from organization.Id

        var results = (await sutProvider.Sut.ValidateManyAsync([BuildRequest(orgUser, organization)])).ToList();

        Assert.Single(results);
        Assert.True(results[0].IsError);
        Assert.IsType<OrganizationUserIdIsInvalid>(results[0].AsError);
    }

    [Theory]
    [BitAutoData(OrganizationUserStatusType.Invited)]
    [BitAutoData(OrganizationUserStatusType.Revoked)]
    [BitAutoData(OrganizationUserStatusType.Confirmed)]
    public async Task ValidateManyAsync_NonAcceptedStatus_ReturnsUserIsNotAcceptedError(
        OrganizationUserStatusType status,
        SutProvider<BulkAutomaticallyConfirmOrganizationUsersValidator> sutProvider,
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser,
        Guid userId)
    {
        orgUser.UserId = userId;
        orgUser.OrganizationId = organization.Id;
        orgUser.Status = status;

        var results = (await sutProvider.Sut.ValidateManyAsync([BuildRequest(orgUser, organization)])).ToList();

        Assert.Single(results);
        Assert.True(results[0].IsError);
        Assert.IsType<UserIsNotAccepted>(results[0].AsError);
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.Custom)]
    public async Task ValidateManyAsync_NonUserType_ReturnsUserIsNotUserTypeError(
        OrganizationUserType type,
        SutProvider<BulkAutomaticallyConfirmOrganizationUsersValidator> sutProvider,
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser,
        Guid userId)
    {
        orgUser.UserId = userId;
        orgUser.OrganizationId = organization.Id;
        orgUser.Type = type;

        var results = (await sutProvider.Sut.ValidateManyAsync([BuildRequest(orgUser, organization)])).ToList();

        Assert.Single(results);
        Assert.True(results[0].IsError);
        Assert.IsType<UserIsNotUserType>(results[0].AsError);
    }

    // ─── Policy disabled ─────────────────────────────────────────────────────────

    [Theory, BitAutoData]
    public async Task ValidateManyAsync_PolicyDisabled_ReturnsAutoConfirmPolicyNotEnabledError(
        SutProvider<BulkAutomaticallyConfirmOrganizationUsersValidator> sutProvider,
        [Organization(useAutomaticUserConfirmation: true)] Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser,
        Guid userId,
        [Policy(PolicyType.AutomaticUserConfirmation, false)] PolicyStatus disabledPolicy)
    {
        orgUser.UserId = userId;
        orgUser.OrganizationId = organization.Id;

        SetupPassingBulkDataStubs(sutProvider, organization, [orgUser], disabledPolicy);

        var results = (await sutProvider.Sut.ValidateManyAsync([BuildRequest(orgUser, organization)])).ToList();

        Assert.Single(results);
        Assert.True(results[0].IsError);
        Assert.IsType<AutomaticallyConfirmUsersPolicyIsNotEnabled>(results[0].AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateManyAsync_UseAutomaticUserConfirmationFalse_ReturnsAutoConfirmPolicyNotEnabledError(
        SutProvider<BulkAutomaticallyConfirmOrganizationUsersValidator> sutProvider,
        [Organization(useAutomaticUserConfirmation: false)] Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser,
        Guid userId,
        [Policy(PolicyType.AutomaticUserConfirmation)] PolicyStatus enabledPolicy)
    {
        orgUser.UserId = userId;
        orgUser.OrganizationId = organization.Id;

        SetupPassingBulkDataStubs(sutProvider, organization, [orgUser], enabledPolicy);

        var results = (await sutProvider.Sut.ValidateManyAsync([BuildRequest(orgUser, organization)])).ToList();

        Assert.Single(results);
        Assert.True(results[0].IsError);
        Assert.IsType<AutomaticallyConfirmUsersPolicyIsNotEnabled>(results[0].AsError);
    }

    // ─── 2FA enforcement ─────────────────────────────────────────────────────────

    [Theory, BitAutoData]
    public async Task ValidateManyAsync_UserWithout2FA_And2FARequired_ReturnsUserDoesNotHaveTwoFactorEnabledError(
        SutProvider<BulkAutomaticallyConfirmOrganizationUsersValidator> sutProvider,
        [Organization(useAutomaticUserConfirmation: true)] Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser,
        Guid userId,
        [Policy(PolicyType.AutomaticUserConfirmation)] PolicyStatus enabledPolicy)
    {
        orgUser.UserId = userId;
        orgUser.OrganizationId = organization.Id;

        SetupPassingBulkDataStubs(sutProvider, organization, [orgUser], enabledPolicy);

        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([(userId, false)]);

        var twoFactorPolicyDetails = new PolicyDetails
        {
            OrganizationId = organization.Id,
            PolicyType = PolicyType.TwoFactorAuthentication
        };
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<RequireTwoFactorPolicyRequirement>(Arg.Any<IEnumerable<Guid>>())
            .Returns([(userId, new RequireTwoFactorPolicyRequirement([twoFactorPolicyDetails]))]);

        var results = (await sutProvider.Sut.ValidateManyAsync([BuildRequest(orgUser, organization)])).ToList();

        Assert.Single(results);
        Assert.True(results[0].IsError);
        Assert.IsType<UserDoesNotHaveTwoFactorEnabled>(results[0].AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateManyAsync_UserWithout2FA_And2FANotRequired_ReturnsValidResult(
        SutProvider<BulkAutomaticallyConfirmOrganizationUsersValidator> sutProvider,
        [Organization(useAutomaticUserConfirmation: true)] Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser,
        Guid userId,
        [Policy(PolicyType.AutomaticUserConfirmation)] PolicyStatus enabledPolicy)
    {
        orgUser.UserId = userId;
        orgUser.OrganizationId = organization.Id;

        SetupPassingBulkDataStubs(sutProvider, organization, [orgUser], enabledPolicy);

        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([(userId, false)]);

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<RequireTwoFactorPolicyRequirement>(Arg.Any<IEnumerable<Guid>>())
            .Returns([(userId, new RequireTwoFactorPolicyRequirement([]))]);

        var results = (await sutProvider.Sut.ValidateManyAsync([BuildRequest(orgUser, organization)])).ToList();

        Assert.Single(results);
        Assert.True(results[0].IsValid);
    }

    // ─── Provider-user rejection ─────────────────────────────────────────────────

    [Theory, BitAutoData]
    public async Task ValidateManyAsync_ProviderUser_ReturnsProviderUsersCannotJoinError(
        SutProvider<BulkAutomaticallyConfirmOrganizationUsersValidator> sutProvider,
        [Organization(useAutomaticUserConfirmation: true)] Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser,
        Guid userId,
        [Policy(PolicyType.AutomaticUserConfirmation)] PolicyStatus enabledPolicy)
    {
        orgUser.UserId = userId;
        orgUser.OrganizationId = organization.Id;

        SetupPassingBulkDataStubs(sutProvider, organization, [orgUser], enabledPolicy);

        // AutoConfirm policy is enabled for this org (triggers provider + multi-org checks).
        var autoConfirmDetails = new PolicyDetails { OrganizationId = organization.Id };
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(Arg.Any<IEnumerable<Guid>>())
            .Returns([(userId, new AutomaticUserConfirmationPolicyRequirement([autoConfirmDetails]))]);

        sutProvider.GetDependency<IProviderUserRepository>()
            .GetManyByManyUsersAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([new ProviderUser { UserId = userId }]);

        var results = (await sutProvider.Sut.ValidateManyAsync([BuildRequest(orgUser, organization)])).ToList();

        Assert.Single(results);
        Assert.True(results[0].IsError);
        Assert.IsType<ProviderUsersCannotJoin>(results[0].AsError);
    }

    // ─── Cross-org membership > 1 ────────────────────────────────────────────────

    [Theory, BitAutoData]
    public async Task ValidateManyAsync_CrossOrgMembershipsGreaterThanOne_ReturnsUserCannotBelongToAnotherOrganizationError(
        SutProvider<BulkAutomaticallyConfirmOrganizationUsersValidator> sutProvider,
        [Organization(useAutomaticUserConfirmation: true)] Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser,
        [OrganizationUser(OrganizationUserStatusType.Confirmed)] OrganizationUser otherOrgUser,
        Guid userId,
        [Policy(PolicyType.AutomaticUserConfirmation)] PolicyStatus enabledPolicy)
    {
        orgUser.UserId = userId;
        orgUser.OrganizationId = organization.Id;
        otherOrgUser.UserId = userId;
        otherOrgUser.OrganizationId = Guid.NewGuid();

        SetupPassingBulkDataStubs(sutProvider, organization, [orgUser], enabledPolicy);

        var autoConfirmDetails = new PolicyDetails { OrganizationId = organization.Id };
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(Arg.Any<IEnumerable<Guid>>())
            .Returns([(userId, new AutomaticUserConfirmationPolicyRequirement([autoConfirmDetails]))]);

        // Two org memberships for the same userId.
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByManyUsersAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([orgUser, otherOrgUser]);

        var results = (await sutProvider.Sut.ValidateManyAsync([BuildRequest(orgUser, organization)])).ToList();

        Assert.Single(results);
        Assert.True(results[0].IsError);
        Assert.IsType<UserCannotBelongToAnotherOrganization>(results[0].AsError);
    }

    // ─── IsEnabledForOrganizationsOtherThan ─────────────────────────────────────

    [Theory, BitAutoData]
    public async Task ValidateManyAsync_OtherOrgHasAutoConfirmPolicy_ReturnsOtherOrganizationDoesNotAllowOtherMembershipError(
        SutProvider<BulkAutomaticallyConfirmOrganizationUsersValidator> sutProvider,
        [Organization(useAutomaticUserConfirmation: true)] Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser,
        Guid userId,
        [Policy(PolicyType.AutomaticUserConfirmation)] PolicyStatus enabledPolicy)
    {
        orgUser.UserId = userId;
        orgUser.OrganizationId = organization.Id;

        SetupPassingBulkDataStubs(sutProvider, organization, [orgUser], enabledPolicy);

        // AutoConfirm policy is enabled for a *different* org, not the target org.
        var otherOrgDetails = new PolicyDetails { OrganizationId = Guid.NewGuid() };
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(Arg.Any<IEnumerable<Guid>>())
            .Returns([(userId, new AutomaticUserConfirmationPolicyRequirement([otherOrgDetails]))]);

        var results = (await sutProvider.Sut.ValidateManyAsync([BuildRequest(orgUser, organization)])).ToList();

        Assert.Single(results);
        Assert.True(results[0].IsError);
        Assert.IsType<OtherOrganizationDoesNotAllowOtherMembership>(results[0].AsError);
    }

    // ─── Happy path ──────────────────────────────────────────────────────────────

    [Theory, BitAutoData]
    public async Task ValidateManyAsync_ValidUser_ReturnsValidResult(
        SutProvider<BulkAutomaticallyConfirmOrganizationUsersValidator> sutProvider,
        [Organization(useAutomaticUserConfirmation: true, planType: PlanType.EnterpriseAnnually)] Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser,
        Guid userId,
        [Policy(PolicyType.AutomaticUserConfirmation)] PolicyStatus enabledPolicy)
    {
        orgUser.UserId = userId;
        orgUser.OrganizationId = organization.Id;

        SetupPassingBulkDataStubs(sutProvider, organization, [orgUser], enabledPolicy);

        var results = (await sutProvider.Sut.ValidateManyAsync([BuildRequest(orgUser, organization)])).ToList();

        Assert.Single(results);
        Assert.True(results[0].IsValid);
    }

    // ─── Batch behaviour ─────────────────────────────────────────────────────────

    [Theory, BitAutoData]
    public async Task ValidateManyAsync_MixedBatch_ReturnsPerRequestResults(
        SutProvider<BulkAutomaticallyConfirmOrganizationUsersValidator> sutProvider,
        [Organization(useAutomaticUserConfirmation: true, planType: PlanType.EnterpriseAnnually)] Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser validOrgUser,
        Guid validUserId,
        [Policy(PolicyType.AutomaticUserConfirmation)] PolicyStatus enabledPolicy)
    {
        validOrgUser.UserId = validUserId;
        validOrgUser.OrganizationId = organization.Id;

        // Second request has no UserId — will fail structural check before any DB fetch.
        var invalidRequest = new AutomaticallyConfirmOrganizationUserValidationRequest
        {
            PerformedBy = Substitute.For<IActingUser>(),
            DefaultUserCollectionName = string.Empty,
            OrganizationUser = null,
            OrganizationUserId = Guid.NewGuid(),
            Organization = organization,
            OrganizationId = organization.Id,
            Key = "test-key"
        };

        SetupPassingBulkDataStubs(sutProvider, organization, [validOrgUser], enabledPolicy);

        var results = (await sutProvider.Sut.ValidateManyAsync(
            [BuildRequest(validOrgUser, organization), invalidRequest])).ToList();

        Assert.Equal(2, results.Count);

        var validResult = results.Single(r => r.Request.OrganizationUserId == validOrgUser.Id);
        Assert.True(validResult.IsValid);

        var invalidResult = results.Single(r => r.Request.OrganizationUserId == invalidRequest.OrganizationUserId);
        Assert.True(invalidResult.IsError);
        Assert.IsType<UserNotFoundError>(invalidResult.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateManyAsync_PreservesInputOrder(
        SutProvider<BulkAutomaticallyConfirmOrganizationUsersValidator> sutProvider,
        [Organization(useAutomaticUserConfirmation: true, planType: PlanType.EnterpriseAnnually)] Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser1,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser2,
        Guid userId1,
        Guid userId2,
        [Policy(PolicyType.AutomaticUserConfirmation)] PolicyStatus enabledPolicy)
    {
        orgUser1.UserId = userId1;
        orgUser1.OrganizationId = organization.Id;
        orgUser2.UserId = userId2;
        orgUser2.OrganizationId = organization.Id;

        SetupPassingBulkDataStubs(sutProvider, organization, [orgUser1, orgUser2], enabledPolicy);

        var results = (await sutProvider.Sut.ValidateManyAsync(
            [BuildRequest(orgUser1, organization), BuildRequest(orgUser2, organization)])).ToList();

        Assert.Equal(2, results.Count);
        Assert.Equal(orgUser1.Id, results[0].Request.OrganizationUserId);
        Assert.Equal(orgUser2.Id, results[1].Request.OrganizationUserId);
    }
}
