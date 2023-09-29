using Bit.Core.OrganizationFeatures.OrganizationDomains;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationDomains;

[SutProviderCustomize]
public class GetOrganizationDomainByIdAndOrganizationIdQueryTests
{
    [Theory, BitAutoData]
    public async Task GetOrganizationDomainByIdAndOrganizationIdAsync_CallsGetByIdAndOrganizationIdAsync(Guid id, Guid organizationId,
        SutProvider<GetOrganizationDomainByIdAndOrganizationIdQuery> sutProvider)
    {
        await sutProvider.Sut.GetOrganizationDomainByIdAndOrganizationIdAsync(id, organizationId);

        await sutProvider.GetDependency<IOrganizationDomainRepository>().Received(1)
            .GetDomainByIdAndOrganizationIdAsync(id, organizationId);
    }
}
