using Bit.Commercial.Core.SecretsManager.Queries;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Context;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Commercial.Core.Test.SecretsManager.Queries;

[SutProviderCustomize]
public class SecretsManagerAccessQueryTests
{
    [Theory]
    [BitAutoData]
    public async Task HasAccessAsync_NoClaim_ReturnsFalseWithoutReadingOrganization(
        SutProvider<SecretsManagerAccessQuery> sutProvider, Guid organizationId)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(organizationId).Returns(false);

        var result = await sutProvider.Sut.HasAccessAsync(organizationId);

        Assert.False(result);
        await sutProvider.GetDependency<IOrganizationRepository>().DidNotReceiveWithAnyArgs().GetByIdAsync(default);
    }

    [Theory]
    [BitAutoData]
    public async Task HasAccessAsync_OrganizationNotFound_ReturnsFalse(
        SutProvider<SecretsManagerAccessQuery> sutProvider, Guid organizationId)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(organizationId).Returns(true);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns((Organization?)null);

        var result = await sutProvider.Sut.HasAccessAsync(organizationId);

        Assert.False(result);
    }

    [Theory]
    [BitAutoData(false, true)]
    [BitAutoData(true, false)]
    [BitAutoData(false, false)]
    public async Task HasAccessAsync_OrganizationNoLongerEligible_ReturnsFalse(
        bool useSecretsManager, bool enabled,
        SutProvider<SecretsManagerAccessQuery> sutProvider, Organization organization)
    {
        organization.UseSecretsManager = useSecretsManager;
        organization.Enabled = enabled;
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(organization.Id).Returns(true);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);

        var result = await sutProvider.Sut.HasAccessAsync(organization.Id);

        Assert.False(result);
    }

    [Theory]
    [BitAutoData]
    public async Task HasAccessAsync_ClaimAndOrganizationEligible_ReturnsTrue(
        SutProvider<SecretsManagerAccessQuery> sutProvider, Organization organization)
    {
        organization.UseSecretsManager = true;
        organization.Enabled = true;
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(organization.Id).Returns(true);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);

        var result = await sutProvider.Sut.HasAccessAsync(organization.Id);

        Assert.True(result);
    }

    [Theory]
    [BitAutoData]
    public async Task HasAccessAsync_SameOrganizationTwice_ReadsOrganizationOnce(
        SutProvider<SecretsManagerAccessQuery> sutProvider, Organization organization)
    {
        organization.UseSecretsManager = true;
        organization.Enabled = true;
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(organization.Id).Returns(true);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);

        var first = await sutProvider.Sut.HasAccessAsync(organization.Id);
        var second = await sutProvider.Sut.HasAccessAsync(organization.Id);

        Assert.True(first);
        Assert.True(second);
        await sutProvider.GetDependency<IOrganizationRepository>().Received(1).GetByIdAsync(organization.Id);
    }
}
