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
using Bit.Core.Test.AdminConsole.AutoFixture;
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
        [OrganizationUserPolicyDetails(PolicyType.SingleOrg,
            OrganizationUserStatusType.Revoked,
            OrganizationUserType.Admin,
            false)] OrganizationUserPolicyDetails singleOrgPolicyDetails,
        [OrganizationUserPolicyDetails(PolicyType.AutomaticUserConfirmation)] OrganizationUserPolicyDetails autoConfirmPolicyDetails,
        SutProvider<PolicyService> sutProvider)
    {
        // Arrange
        singleOrgPolicyDetails.OrganizationUserStatus = OrganizationUserStatusType.Revoked;
        singleOrgPolicyDetails.OrganizationUserType = OrganizationUserType.Owner;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByUserIdWithPolicyDetailsAsync(userId, PolicyType.SingleOrg)
            .Returns([singleOrgPolicyDetails]);

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.AutomaticConfirmUsers)
            .Returns(true);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByUserIdWithPolicyDetailsAsync(userId, PolicyType.AutomaticUserConfirmation)
            .Returns([autoConfirmPolicyDetails]);

        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilitiesAsync()
            .Returns(new Dictionary<Guid, OrganizationAbility>()
            {
                {
                    singleOrgPolicyDetails.OrganizationId,
                    new OrganizationAbility
                    {
                        Id = singleOrgPolicyDetails.OrganizationId,
                        UsePolicies = true
                    }
                }
            });

        // Act
        var result = await sutProvider.Sut
            .GetPoliciesApplicableToUserAsync(userId, PolicyType.SingleOrg);

        // Assert - Should include Revoked user because auto-confirm is enabled
        Assert.Single(result);
        Assert.Contains(result, p => p.OrganizationUserStatus == singleOrgPolicyDetails.OrganizationUserStatus);
        Assert.Contains(result, p => p.OrganizationUserType == OrganizationUserType.Owner);
    }

    [Theory, BitAutoData]
    public async Task GetPoliciesApplicableToUserAsync_WithAutoConfirmEnabled_WithSingleOrgPolicy_IncludesOwnerAndAdmin(
        Guid userId,
        Guid organizationId,
        [OrganizationUserPolicyDetails(PolicyType.SingleOrg, OrganizationUserStatusType.Confirmed, OrganizationUserType.Admin, false)] OrganizationUserPolicyDetails admin,
        [OrganizationUserPolicyDetails(PolicyType.SingleOrg, OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner, false)] OrganizationUserPolicyDetails owner,
        [OrganizationUserPolicyDetails(PolicyType.SingleOrg, OrganizationUserStatusType.Confirmed, OrganizationUserType.User, false)] OrganizationUserPolicyDetails user,
        SutProvider<PolicyService> sutProvider)
    {
        owner.OrganizationId = admin.OrganizationId = user.OrganizationId = organizationId;

        // Arrange - Setup SingleOrg policy with Owner and Admin users (normally excluded from SingleOrg)
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByUserIdWithPolicyDetailsAsync(userId, PolicyType.SingleOrg)
            .Returns([admin, owner, user]);

        // Enable AutomaticConfirmUsers feature flag
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.AutomaticConfirmUsers)
            .Returns(true);

        // Mock repository call - user has AutomaticUserConfirmation policy details
        var autoConfirmPolicies = new List<OrganizationUserPolicyDetails>
        {
            new() { OrganizationId = organizationId, PolicyType = PolicyType.AutomaticUserConfirmation, PolicyEnabled = true, OrganizationUserType = OrganizationUserType.User, OrganizationUserStatus = OrganizationUserStatusType.Confirmed, IsProvider = false }
        };
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByUserIdWithPolicyDetailsAsync(userId, PolicyType.AutomaticUserConfirmation)
            .Returns(autoConfirmPolicies);

        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilitiesAsync()
            .Returns(new Dictionary<Guid, OrganizationAbility>
            {
                { organizationId, new OrganizationAbility { Id = organizationId, UsePolicies = true } }
            });

        // Act
        var result = await sutProvider.Sut
            .GetPoliciesApplicableToUserAsync(userId, PolicyType.SingleOrg);

        // Assert - Should include Owner and Admin because excludedUserTypes is empty when auto-confirm is enabled
        Assert.Equal(3, result.Count);
        Assert.Contains(result, p => p.OrganizationUserType == OrganizationUserType.Owner);
        Assert.Contains(result, p => p.OrganizationUserType == OrganizationUserType.Admin);
        Assert.Contains(result, p => p.OrganizationUserType == OrganizationUserType.User);
    }

    [Theory, BitAutoData]
    public async Task GetPoliciesApplicableToUserAsync_WithAutoConfirmDisabled_WithSingleOrgPolicy_ExcludesRevokedUsers(
        Guid userId,
        Guid organizationId,
        [OrganizationUserPolicyDetails(PolicyType.SingleOrg, OrganizationUserStatusType.Revoked, OrganizationUserType.User, false)] OrganizationUserPolicyDetails revoked,
        [OrganizationUserPolicyDetails(PolicyType.SingleOrg, OrganizationUserStatusType.Confirmed, OrganizationUserType.User, false)] OrganizationUserPolicyDetails confirmed,
        SutProvider<PolicyService> sutProvider)
    {
        revoked.OrganizationId = confirmed.OrganizationId = organizationId;

        // Arrange
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByUserIdWithPolicyDetailsAsync(userId, PolicyType.SingleOrg)
            .Returns([revoked, confirmed]);

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.AutomaticConfirmUsers)
            .Returns(false);

        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilitiesAsync()
            .Returns(new Dictionary<Guid, OrganizationAbility>
            {
                { organizationId, new OrganizationAbility { Id = organizationId, UsePolicies = true } }
            });

        // Act
        var result = await sutProvider.Sut
            .GetPoliciesApplicableToUserAsync(userId, PolicyType.SingleOrg);

        // Assert
        Assert.Single(result);
        Assert.DoesNotContain(result, p => p.OrganizationUserStatus == OrganizationUserStatusType.Revoked);
        Assert.DoesNotContain(result, p => p.OrganizationUserStatus == OrganizationUserStatusType.Invited);
        Assert.Contains(result, p => p.OrganizationUserStatus == confirmed.OrganizationUserStatus);
    }

    [Theory, BitAutoData]
    public async Task GetPoliciesApplicableToUserAsync_WithAutoConfirmEnabled_NoAutoConfirmPolicy_ExcludesOwnerAndAdmin(
        Guid userId,
        Guid organizationId,
        [OrganizationUserPolicyDetails(PolicyType.SingleOrg, OrganizationUserStatusType.Revoked, OrganizationUserType.Admin, false)] OrganizationUserPolicyDetails admin,
        [OrganizationUserPolicyDetails(PolicyType.SingleOrg, OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner, false)] OrganizationUserPolicyDetails owner,
        [OrganizationUserPolicyDetails(PolicyType.SingleOrg, OrganizationUserStatusType.Confirmed, OrganizationUserType.User, false)] OrganizationUserPolicyDetails user,
        SutProvider<PolicyService> sutProvider)
    {
        // Arrange
        user.OrganizationId = admin.OrganizationId = owner.OrganizationId = organizationId;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByUserIdWithPolicyDetailsAsync(userId, PolicyType.SingleOrg)
            .Returns([admin, owner, user]);

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.AutomaticConfirmUsers)
            .Returns(true);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByUserIdWithPolicyDetailsAsync(userId, PolicyType.AutomaticUserConfirmation)
            .Returns([]);

        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilitiesAsync()
            .Returns(new Dictionary<Guid, OrganizationAbility>
            {
                { organizationId, new OrganizationAbility { Id = organizationId, UsePolicies = true } }
            });

        // Act
        var result = await sutProvider.Sut
            .GetPoliciesApplicableToUserAsync(userId, PolicyType.SingleOrg);

        // Assert
        Assert.Single(result);
        Assert.DoesNotContain(result, p => p.OrganizationUserType == OrganizationUserType.Owner);
        Assert.DoesNotContain(result, p => p.OrganizationUserType == OrganizationUserType.Admin);
        Assert.All(result, p => Assert.Equal(user.OrganizationUserType, p.OrganizationUserType));
    }

    [Theory, BitAutoData]
    public async Task GetPoliciesApplicableToUserAsync_WithNonSingleOrgPolicy_IgnoresAutoConfirmSettings(
        Guid userId,
        Guid organizationId,
        [OrganizationUserPolicyDetails(PolicyType.DisableSend)] OrganizationUserPolicyDetails disableSendPolicy,
        SutProvider<PolicyService> sutProvider)
    {
        // Arrange
        disableSendPolicy.OrganizationId = organizationId;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByUserIdWithPolicyDetailsAsync(userId, PolicyType.DisableSend)
            .Returns([disableSendPolicy]);

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.AutomaticConfirmUsers)
            .Returns(true);

        var autoConfirmPolicies = new List<OrganizationUserPolicyDetails>
        {
            new() { OrganizationId = Guid.NewGuid(), PolicyType = PolicyType.AutomaticUserConfirmation, PolicyEnabled = true, OrganizationUserType = OrganizationUserType.User, OrganizationUserStatus = OrganizationUserStatusType.Confirmed, IsProvider = false }
        };
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByUserIdWithPolicyDetailsAsync(userId, PolicyType.AutomaticUserConfirmation)
            .Returns(autoConfirmPolicies);

        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilitiesAsync()
            .Returns(new Dictionary<Guid, OrganizationAbility>
            {
                { organizationId, new OrganizationAbility { Id = organizationId, UsePolicies = true } }
            });

        // Act
        var result = await sutProvider.Sut
            .GetPoliciesApplicableToUserAsync(userId, PolicyType.DisableSend);

        // Assert
        Assert.Single(result);
        Assert.DoesNotContain(result, p => p.OrganizationUserStatus == OrganizationUserStatusType.Revoked);
        Assert.All(result, p => Assert.Equal(disableSendPolicy.OrganizationUserStatus, p.OrganizationUserStatus));
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
