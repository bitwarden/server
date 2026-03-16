using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Implementations;
using Bit.Core.AdminConsole.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.Policies;

[SutProviderCustomize]
public class PolicyQueryTests
{
    [Theory, BitAutoData]
    public async Task RunAsync_WithExistingPolicy_ReturnsPolicy(SutProvider<PolicyQuery> sutProvider,
        Policy policy)
    {
        // Arrange
        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(policy.OrganizationId, policy.Type)
            .Returns(policy);

        // Act
        var policyData = await sutProvider.Sut.RunAsync(policy.OrganizationId, policy.Type);

        // Assert
        Assert.Equal(policy.Data, policyData.Data);
        Assert.Equal(policy.Type, policyData.Type);
        Assert.Equal(policy.Enabled, policyData.Enabled);
        Assert.Equal(policy.OrganizationId, policyData.OrganizationId);
    }

    [Theory, BitAutoData]
    public async Task RunAsync_WithNonExistentPolicy_ReturnsDefaultDisabledPolicy(
        SutProvider<PolicyQuery> sutProvider,
        Guid organizationId,
        PolicyType policyType)
    {
        // Arrange
        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(organizationId, policyType)
            .ReturnsNull();

        // Act
        var policyData = await sutProvider.Sut.RunAsync(organizationId, policyType);

        // Assert
        Assert.Equal(organizationId, policyData.OrganizationId);
        Assert.Equal(policyType, policyData.Type);
        Assert.False(policyData.Enabled);
        Assert.Null(policyData.Data);
    }
}
