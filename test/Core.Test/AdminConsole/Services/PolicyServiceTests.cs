using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Services.Implementations;
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
