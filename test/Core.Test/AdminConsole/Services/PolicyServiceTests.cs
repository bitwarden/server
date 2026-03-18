using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Services.Implementations;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
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
    public async Task GetMasterPasswordPolicyForUserAsync_ReturnsEnforcedOptions(User user, SutProvider<PolicyService> sutProvider)
    {
        // Arrange: Create three policies with different requirements to test combining behavior
        var policy1 = new Policy
        {
            Id = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            Type = PolicyType.MasterPassword,
            Enabled = true
        };
        policy1.SetDataModel(new MasterPasswordPolicyData
        {
            MinComplexity = 3,
            MinLength = 12,
            RequireLower = true,
            RequireUpper = false,
            RequireNumbers = true,
            RequireSpecial = false,
            EnforceOnLogin = true
        });

        var policy2 = new Policy
        {
            Id = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            Type = PolicyType.MasterPassword,
            Enabled = true
        };
        policy2.SetDataModel(new MasterPasswordPolicyData
        {
            MinComplexity = 4,
            MinLength = 10,
            RequireLower = false,
            RequireUpper = true,
            RequireNumbers = false,
            RequireSpecial = true,
            EnforceOnLogin = false
        });

        var policy3 = new Policy
        {
            Id = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            Type = PolicyType.MasterPassword,
            Enabled = true
        };
        policy3.SetDataModel(new MasterPasswordPolicyData
        {
            MinComplexity = 2,
            MinLength = 15,
            RequireLower = false,
            RequireUpper = false,
            RequireNumbers = false,
            RequireSpecial = false,
            EnforceOnLogin = false
        });

        sutProvider.GetDependency<IPolicyRepository>()
            .GetManyByUserIdAsync(user.Id)
            .Returns([policy1, policy2, policy3]);

        // Act
        var result = await sutProvider.Sut.GetMasterPasswordPolicyForUserAsync(user);

        // Assert: Verify that policies were combined correctly
        Assert.NotNull(result);

        // MinComplexity and MinLength should take the highest values
        Assert.Equal(4, result.MinComplexity); // highest from policy2
        Assert.Equal(15, result.MinLength); // highest from policy3

        // Boolean flags should use OR logic (true if any policy has true)
        Assert.True(result.RequireLower); // true from policy1
        Assert.True(result.RequireUpper); // true from policy2
        Assert.True(result.RequireNumbers); // true from policy1
        Assert.True(result.RequireSpecial); // true from policy2
        Assert.True(result.EnforceOnLogin); // true from policy1
    }

    [Theory, BitAutoData]
    public async Task GetMasterPasswordPolicyForUserAsync_WithNoPolicies_ReturnsNull(User user, SutProvider<PolicyService> sutProvider)
    {
        // Arrange: No enabled policies
        sutProvider.GetDependency<IPolicyRepository>()
            .GetManyByUserIdAsync(user.Id)
            .Returns(new List<Policy>());

        // Act
        var result = await sutProvider.Sut.GetMasterPasswordPolicyForUserAsync(user);

        // Assert
        Assert.Null(result);
    }

    [Theory, BitAutoData]
    public async Task GetMasterPasswordPolicyForUserAsync_WithDisabledPolicies_ReturnsNull(User user, SutProvider<PolicyService> sutProvider)
    {
        // Arrange: Policies exist but are disabled
        var disabledPolicy = new Policy
        {
            Id = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            Type = PolicyType.MasterPassword,
            Enabled = false
        };
        disabledPolicy.SetDataModel(new MasterPasswordPolicyData
        {
            MinComplexity = 3,
            MinLength = 12
        });

        sutProvider.GetDependency<IPolicyRepository>()
            .GetManyByUserIdAsync(user.Id)
            .Returns(new List<Policy> { disabledPolicy });

        // Act
        var result = await sutProvider.Sut.GetMasterPasswordPolicyForUserAsync(user);

        // Assert
        Assert.Null(result);
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
