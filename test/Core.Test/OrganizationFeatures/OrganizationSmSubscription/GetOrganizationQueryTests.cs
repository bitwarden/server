using Bit.Core.Entities;
using Bit.Core.OrganizationFeatures.OrganizationSmSubscription;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationSmSubscription;

[SutProviderCustomize]
public class GetOrganizationQueryTests
{
    [Theory]
    [BitAutoData]
    public async Task GetOrgById_OrganizationNotFound_ReturnsNull(SutProvider<GetOrganizationQuery> sutProvider)
    {
        var organizationId = Guid.NewGuid();

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId)
            .Returns((Organization)null);

        var result = await sutProvider.Sut.GetOrgById(organizationId);

        Assert.Null(result);
    }

    [Theory]
    [BitAutoData]
    public async Task GetOrgById_ReturnsOrganization(SutProvider<GetOrganizationQuery> sutProvider, Organization organization, Guid organizationId)
    {
        //var organizationId = Guid.NewGuid();

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId)
            .Returns(organization);

        var result = await sutProvider.Sut.GetOrgById(organizationId);

        Assert.Equal(organization, result);
    }
}
