using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Services.Implementations;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;
using GlobalSettings = Bit.Core.Settings.GlobalSettings;

namespace Bit.Core.Test.AdminConsole.Services;

[SutProviderCustomize]
public class PolicyServiceTests
{
    [Theory, BitAutoData]
    public async Task GetPoliciesApplicableToUserAsync_WithRequireSsoTypeFilter_WithDefaultOrganizationUserStatusFilter_ReturnsNoPolicies(Guid userId, SutProvider<PolicyService> sutProvider)
    {
        SetupUserPolicies(userId, sutProvider);

        var result = await sutProvider.Sut
            .GetPoliciesApplicableToUserAsync(userId, PolicyType.RequireSso);

        Assert.Empty(result);
    }

    [Theory, BitAutoData]
    public async Task GetPoliciesApplicableToUserAsync_WithRequireSsoTypeFilter_WithDefaultOrganizationUserStatusFilter_ReturnsOnePolicy(Guid userId, SutProvider<PolicyService> sutProvider)
    {
        SetupUserPolicies(userId, sutProvider);

        sutProvider.GetDependency<GlobalSettings>().Sso.EnforceSsoPolicyForAllUsers.Returns(true);

        var result = await sutProvider.Sut
            .GetPoliciesApplicableToUserAsync(userId, PolicyType.RequireSso);

        Assert.Single(result);
        Assert.True(result.All(details => details.PolicyEnabled));
        Assert.True(result.All(details => details.PolicyType == PolicyType.RequireSso));
        Assert.True(result.All(details => details.OrganizationUserType == OrganizationUserType.Owner));
        Assert.True(result.All(details => details.OrganizationUserStatus == OrganizationUserStatusType.Confirmed));
        Assert.True(result.All(details => !details.IsProvider));
    }

    [Theory, BitAutoData]
    public async Task GetPoliciesApplicableToUserAsync_WithDisableTypeFilter_WithDefaultOrganizationUserStatusFilter_ReturnsNoPolicies(Guid userId, SutProvider<PolicyService> sutProvider)
    {
        SetupUserPolicies(userId, sutProvider);

        var result = await sutProvider.Sut
            .GetPoliciesApplicableToUserAsync(userId, PolicyType.DisableSend);

        Assert.Empty(result);
    }

    [Theory, BitAutoData]
    public async Task GetPoliciesApplicableToUserAsync_WithDisableSendTypeFilter_WithInvitedUserStatusFilter_ReturnsOnePolicy(Guid userId, SutProvider<PolicyService> sutProvider)
    {
        SetupUserPolicies(userId, sutProvider);

        var result = await sutProvider.Sut
            .GetPoliciesApplicableToUserAsync(userId, PolicyType.DisableSend, OrganizationUserStatusType.Invited);

        Assert.Single(result);
        Assert.True(result.All(details => details.PolicyEnabled));
        Assert.True(result.All(details => details.PolicyType == PolicyType.DisableSend));
        Assert.True(result.All(details => details.OrganizationUserType == OrganizationUserType.User));
        Assert.True(result.All(details => details.OrganizationUserStatus == OrganizationUserStatusType.Invited));
        Assert.True(result.All(details => !details.IsProvider));
    }

    [Theory, BitAutoData]
    public async Task AnyPoliciesApplicableToUserAsync_WithRequireSsoTypeFilter_WithDefaultOrganizationUserStatusFilter_ReturnsFalse(Guid userId, SutProvider<PolicyService> sutProvider)
    {
        SetupUserPolicies(userId, sutProvider);

        var result = await sutProvider.Sut
            .AnyPoliciesApplicableToUserAsync(userId, PolicyType.RequireSso);

        Assert.False(result);
    }

    [Theory, BitAutoData]
    public async Task AnyPoliciesApplicableToUserAsync_WithRequireSsoTypeFilter_WithDefaultOrganizationUserStatusFilter_ReturnsTrue(Guid userId, SutProvider<PolicyService> sutProvider)
    {
        SetupUserPolicies(userId, sutProvider);

        sutProvider.GetDependency<GlobalSettings>().Sso.EnforceSsoPolicyForAllUsers.Returns(true);

        var result = await sutProvider.Sut
            .AnyPoliciesApplicableToUserAsync(userId, PolicyType.RequireSso);

        Assert.True(result);
    }

    [Theory, BitAutoData]
    public async Task AnyPoliciesApplicableToUserAsync_WithDisableTypeFilter_WithDefaultOrganizationUserStatusFilter_ReturnsFalse(Guid userId, SutProvider<PolicyService> sutProvider)
    {
        SetupUserPolicies(userId, sutProvider);

        var result = await sutProvider.Sut
            .AnyPoliciesApplicableToUserAsync(userId, PolicyType.DisableSend);

        Assert.False(result);
    }

    [Theory, BitAutoData]
    public async Task AnyPoliciesApplicableToUserAsync_WithDisableSendTypeFilter_WithInvitedUserStatusFilter_ReturnsTrue(Guid userId, SutProvider<PolicyService> sutProvider)
    {
        SetupUserPolicies(userId, sutProvider);

        var result = await sutProvider.Sut
            .AnyPoliciesApplicableToUserAsync(userId, PolicyType.DisableSend, OrganizationUserStatusType.Invited);

        Assert.True(result);
    }

    [Theory, BitAutoData]
    public async Task GetPoliciesApplicableToUserAsync_WithAutoConfirmEnabled_WithSingleOrgPolicy_IncludesRevokedUsers(
        Guid userId,
        SutProvider<PolicyService> sutProvider)
    {
        // Arrange - Setup SingleOrg policy with Revoked user
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByUserIdWithPolicyDetailsAsync(userId, PolicyType.SingleOrg)
            .Returns(new List<OrganizationUserPolicyDetails>
            {
                new() { OrganizationId = Guid.NewGuid(), PolicyType = PolicyType.SingleOrg, PolicyEnabled = true, OrganizationUserType = OrganizationUserType.User, OrganizationUserStatus = OrganizationUserStatusType.Revoked, IsProvider = false },
                new() { OrganizationId = Guid.NewGuid(), PolicyType = PolicyType.SingleOrg, PolicyEnabled = true, OrganizationUserType = OrganizationUserType.Owner, OrganizationUserStatus = OrganizationUserStatusType.Confirmed, IsProvider = false }
            });

        // Enable AutomaticConfirmUsers feature flag
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.AutomaticConfirmUsers)
            .Returns(true);

        // Setup recursive call - user has AutomaticUserConfirmation policy
        var autoConfirmPolicies = new List<OrganizationUserPolicyDetails>
        {
            new() { OrganizationId = Guid.NewGuid(), PolicyType = PolicyType.AutomaticUserConfirmation, PolicyEnabled = true, OrganizationUserType = OrganizationUserType.User, OrganizationUserStatus = OrganizationUserStatusType.Revoked, IsProvider = false }
        };
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByUserIdWithPolicyDetailsAsync(userId, PolicyType.AutomaticUserConfirmation)
            .Returns(autoConfirmPolicies);

        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilitiesAsync()
            .Returns(Task.FromResult<IDictionary<Guid, OrganizationAbility>>(
                new Dictionary<Guid, OrganizationAbility>()));

        // Act
        var result = await sutProvider.Sut
            .GetPoliciesApplicableToUserAsync(userId, PolicyType.SingleOrg);

        // Assert - Should include Revoked user because auto-confirm is enabled
        Assert.Equal(2, result.Count());
        Assert.Contains(result, p => p.OrganizationUserStatus == OrganizationUserStatusType.Revoked);
        Assert.Contains(result, p => p.OrganizationUserType == OrganizationUserType.Owner);
    }

    [Theory, BitAutoData]
    public async Task GetPoliciesApplicableToUserAsync_WithAutoConfirmEnabled_WithSingleOrgPolicy_IncludesOwnerAndAdmin(
        Guid userId,
        SutProvider<PolicyService> sutProvider)
    {
        // Arrange - Setup SingleOrg policy with Owner and Admin users (normally excluded from SingleOrg)
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByUserIdWithPolicyDetailsAsync(userId, PolicyType.SingleOrg)
            .Returns(new List<OrganizationUserPolicyDetails>
            {
                new() { OrganizationId = Guid.NewGuid(), PolicyType = PolicyType.SingleOrg, PolicyEnabled = true, OrganizationUserType = OrganizationUserType.Owner, OrganizationUserStatus = OrganizationUserStatusType.Confirmed, IsProvider = false },
                new() { OrganizationId = Guid.NewGuid(), PolicyType = PolicyType.SingleOrg, PolicyEnabled = true, OrganizationUserType = OrganizationUserType.Admin, OrganizationUserStatus = OrganizationUserStatusType.Confirmed, IsProvider = false },
                new() { OrganizationId = Guid.NewGuid(), PolicyType = PolicyType.SingleOrg, PolicyEnabled = true, OrganizationUserType = OrganizationUserType.User, OrganizationUserStatus = OrganizationUserStatusType.Confirmed, IsProvider = false }
            });

        // Enable AutomaticConfirmUsers feature flag
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.AutomaticConfirmUsers)
            .Returns(true);

        // Setup recursive call - user has AutomaticUserConfirmation policy
        var autoConfirmPolicies = new List<OrganizationUserPolicyDetails>
        {
            new() { OrganizationId = Guid.NewGuid(), PolicyType = PolicyType.AutomaticUserConfirmation, PolicyEnabled = true, OrganizationUserType = OrganizationUserType.User, OrganizationUserStatus = OrganizationUserStatusType.Confirmed, IsProvider = false }
        };
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByUserIdWithPolicyDetailsAsync(userId, PolicyType.AutomaticUserConfirmation)
            .Returns(autoConfirmPolicies);

        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilitiesAsync()
            .Returns(Task.FromResult<IDictionary<Guid, OrganizationAbility>>(
                new Dictionary<Guid, OrganizationAbility>()));

        // Act
        var result = await sutProvider.Sut
            .GetPoliciesApplicableToUserAsync(userId, PolicyType.SingleOrg);

        // Assert - Should include Owner and Admin because excludedUserTypes is empty when auto-confirm is enabled
        Assert.Equal(3, result.Count());
        Assert.Contains(result, p => p.OrganizationUserType == OrganizationUserType.Owner);
        Assert.Contains(result, p => p.OrganizationUserType == OrganizationUserType.Admin);
        Assert.Contains(result, p => p.OrganizationUserType == OrganizationUserType.User);
    }

    [Theory, BitAutoData]
    public async Task GetPoliciesApplicableToUserAsync_WithAutoConfirmDisabled_WithSingleOrgPolicy_ExcludesRevokedUsers(
        Guid userId,
        SutProvider<PolicyService> sutProvider)
    {
        // Arrange - Setup SingleOrg policy with Revoked and Confirmed users
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByUserIdWithPolicyDetailsAsync(userId, PolicyType.SingleOrg)
            .Returns(new List<OrganizationUserPolicyDetails>
            {
                new() { OrganizationId = Guid.NewGuid(), PolicyType = PolicyType.SingleOrg, PolicyEnabled = true, OrganizationUserType = OrganizationUserType.User, OrganizationUserStatus = OrganizationUserStatusType.Revoked, IsProvider = false },
                new() { OrganizationId = Guid.NewGuid(), PolicyType = PolicyType.SingleOrg, PolicyEnabled = true, OrganizationUserType = OrganizationUserType.User, OrganizationUserStatus = OrganizationUserStatusType.Confirmed, IsProvider = false }
            });

        // Disable AutomaticConfirmUsers feature flag
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.AutomaticConfirmUsers)
            .Returns(false);

        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilitiesAsync()
            .Returns(Task.FromResult<IDictionary<Guid, OrganizationAbility>>(
                new Dictionary<Guid, OrganizationAbility>()));

        // Act
        var result = await sutProvider.Sut
            .GetPoliciesApplicableToUserAsync(userId, PolicyType.SingleOrg);

        // Assert - Should NOT include Revoked user because feature flag is disabled
        Assert.Single(result);
        Assert.DoesNotContain(result, p => p.OrganizationUserStatus == OrganizationUserStatusType.Revoked);
        Assert.All(result, p => Assert.True(p.OrganizationUserStatus >= OrganizationUserStatusType.Accepted));
    }

    [Theory, BitAutoData]
    public async Task GetPoliciesApplicableToUserAsync_WithAutoConfirmEnabled_NoAutoConfirmPolicy_ExcludesOwnerAndAdmin(
        Guid userId,
        SutProvider<PolicyService> sutProvider)
    {
        // Arrange - Setup SingleOrg policy with Owner, Admin, and User
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByUserIdWithPolicyDetailsAsync(userId, PolicyType.SingleOrg)
            .Returns(new List<OrganizationUserPolicyDetails>
            {
                new() { OrganizationId = Guid.NewGuid(), PolicyType = PolicyType.SingleOrg, PolicyEnabled = true, OrganizationUserType = OrganizationUserType.Owner, OrganizationUserStatus = OrganizationUserStatusType.Confirmed, IsProvider = false },
                new() { OrganizationId = Guid.NewGuid(), PolicyType = PolicyType.SingleOrg, PolicyEnabled = true, OrganizationUserType = OrganizationUserType.Admin, OrganizationUserStatus = OrganizationUserStatusType.Confirmed, IsProvider = false },
                new() { OrganizationId = Guid.NewGuid(), PolicyType = PolicyType.SingleOrg, PolicyEnabled = true, OrganizationUserType = OrganizationUserType.User, OrganizationUserStatus = OrganizationUserStatusType.Confirmed, IsProvider = false }
            });

        // Enable AutomaticConfirmUsers feature flag
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.AutomaticConfirmUsers)
            .Returns(true);

        // Setup recursive call - user has NO AutomaticUserConfirmation policy
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByUserIdWithPolicyDetailsAsync(userId, PolicyType.AutomaticUserConfirmation)
            .Returns(new List<OrganizationUserPolicyDetails>());

        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilitiesAsync()
            .Returns(Task.FromResult<IDictionary<Guid, OrganizationAbility>>(
                new Dictionary<Guid, OrganizationAbility>()));

        // Act
        var result = await sutProvider.Sut
            .GetPoliciesApplicableToUserAsync(userId, PolicyType.SingleOrg);

        // Assert - Should NOT include Owner/Admin because user doesn't have auto-confirm policy
        Assert.Single(result);
        Assert.DoesNotContain(result, p => p.OrganizationUserType == OrganizationUserType.Owner);
        Assert.DoesNotContain(result, p => p.OrganizationUserType == OrganizationUserType.Admin);
        Assert.All(result, p => Assert.Equal(OrganizationUserType.User, p.OrganizationUserType));
    }

    [Theory, BitAutoData]
    public async Task GetPoliciesApplicableToUserAsync_WithNonSingleOrgPolicy_IgnoresAutoConfirmSettings(
        Guid userId,
        SutProvider<PolicyService> sutProvider)
    {
        // Arrange - Setup DisableSend policy (not SingleOrg)
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByUserIdWithPolicyDetailsAsync(userId, PolicyType.DisableSend)
            .Returns(new List<OrganizationUserPolicyDetails>
            {
                new() { OrganizationId = Guid.NewGuid(), PolicyType = PolicyType.DisableSend, PolicyEnabled = true, OrganizationUserType = OrganizationUserType.User, OrganizationUserStatus = OrganizationUserStatusType.Revoked, IsProvider = false },
                new() { OrganizationId = Guid.NewGuid(), PolicyType = PolicyType.DisableSend, PolicyEnabled = true, OrganizationUserType = OrganizationUserType.User, OrganizationUserStatus = OrganizationUserStatusType.Confirmed, IsProvider = false }
            });

        // Enable AutomaticConfirmUsers feature flag
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.AutomaticConfirmUsers)
            .Returns(true);

        // User has AutomaticUserConfirmation policy (but we're querying DisableSend, not SingleOrg)
        var autoConfirmPolicies = new List<OrganizationUserPolicyDetails>
        {
            new() { OrganizationId = Guid.NewGuid(), PolicyType = PolicyType.AutomaticUserConfirmation, PolicyEnabled = true, OrganizationUserType = OrganizationUserType.User, OrganizationUserStatus = OrganizationUserStatusType.Confirmed, IsProvider = false }
        };
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByUserIdWithPolicyDetailsAsync(userId, PolicyType.AutomaticUserConfirmation)
            .Returns(autoConfirmPolicies);

        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilitiesAsync()
            .Returns(Task.FromResult<IDictionary<Guid, OrganizationAbility>>(
                new Dictionary<Guid, OrganizationAbility>()));

        // Act
        var result = await sutProvider.Sut
            .GetPoliciesApplicableToUserAsync(userId, PolicyType.DisableSend);

        // Assert - Should NOT include Revoked user because auto-confirm only applies to SingleOrg policy
        Assert.Single(result);
        Assert.DoesNotContain(result, p => p.OrganizationUserStatus == OrganizationUserStatusType.Revoked);
        Assert.All(result, p => Assert.Equal(OrganizationUserStatusType.Confirmed, p.OrganizationUserStatus));
    }

    [Theory, BitAutoData]
    public async Task GetMasterPasswordPolicyForUserAsync_WithFeatureFlagEnabled_EvaluatesPolicyRequirement(User user, SutProvider<PolicyService> sutProvider)
    {
        SetupUserPolicies(user.Id, sutProvider);
        var policyRequirement = new MasterPasswordPolicyRequirement
        {
            Enabled = true,
            EnforcedOptions = new MasterPasswordPolicyData()
        };
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.PolicyRequirements).Returns(true);
        sutProvider.GetDependency<IPolicyRequirementQuery>().GetAsync<MasterPasswordPolicyRequirement>(user.Id).Returns(policyRequirement);

        var result = await sutProvider.Sut.GetMasterPasswordPolicyForUserAsync(user);

        sutProvider.GetDependency<IFeatureService>().Received(1).IsEnabled(FeatureFlagKeys.PolicyRequirements);
        await sutProvider.GetDependency<IPolicyRepository>().DidNotReceive().GetManyByUserIdAsync(user.Id);
        await sutProvider.GetDependency<IPolicyRequirementQuery>().Received(1).GetAsync<MasterPasswordPolicyRequirement>(user.Id);
    }

    [Theory, BitAutoData]
    public async Task GetMasterPasswordPolicyForUserAsync_WithFeatureFlagDisabled_EvaluatesPolicyDetails(User user, SutProvider<PolicyService> sutProvider)
    {
        SetupUserPolicies(user.Id, sutProvider);
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.PolicyRequirements).Returns(false);

        var result = await sutProvider.Sut.GetMasterPasswordPolicyForUserAsync(user);

        sutProvider.GetDependency<IFeatureService>().Received(1).IsEnabled(FeatureFlagKeys.PolicyRequirements);
        await sutProvider.GetDependency<IPolicyRepository>().Received(1).GetManyByUserIdAsync(user.Id);
        await sutProvider.GetDependency<IPolicyRequirementQuery>().DidNotReceive().GetAsync<MasterPasswordPolicyRequirement>(user.Id);
    }

    private static void SetupOrg(SutProvider<PolicyService> sutProvider, Guid organizationId, Organization organization)
    {
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organizationId)
            .Returns(Task.FromResult(organization));
    }

    private static void SetupUserPolicies(Guid userId, SutProvider<PolicyService> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByUserIdWithPolicyDetailsAsync(userId, PolicyType.RequireSso)
            .Returns(new List<OrganizationUserPolicyDetails>
            {
                new() { OrganizationId = Guid.NewGuid(), PolicyType = PolicyType.RequireSso, PolicyEnabled = false, OrganizationUserType = OrganizationUserType.Owner, OrganizationUserStatus = OrganizationUserStatusType.Confirmed, IsProvider = false},
                new() { OrganizationId = Guid.NewGuid(), PolicyType = PolicyType.RequireSso, PolicyEnabled = true, OrganizationUserType = OrganizationUserType.Owner, OrganizationUserStatus = OrganizationUserStatusType.Confirmed, IsProvider = false },
                new() { OrganizationId = Guid.NewGuid(), PolicyType = PolicyType.RequireSso, PolicyEnabled = true, OrganizationUserType = OrganizationUserType.Owner, OrganizationUserStatus = OrganizationUserStatusType.Confirmed, IsProvider = true }
            });

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByUserIdWithPolicyDetailsAsync(userId, PolicyType.DisableSend)
            .Returns(new List<OrganizationUserPolicyDetails>
            {
                new() { OrganizationId = Guid.NewGuid(), PolicyType = PolicyType.DisableSend, PolicyEnabled = true, OrganizationUserType = OrganizationUserType.User, OrganizationUserStatus = OrganizationUserStatusType.Invited, IsProvider = false },
                new() { OrganizationId = Guid.NewGuid(), PolicyType = PolicyType.DisableSend, PolicyEnabled = true, OrganizationUserType = OrganizationUserType.User, OrganizationUserStatus = OrganizationUserStatusType.Invited, IsProvider = true }
            });
    }
}
