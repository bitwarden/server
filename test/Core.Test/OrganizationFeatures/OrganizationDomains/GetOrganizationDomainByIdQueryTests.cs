using Bit.Core.OrganizationFeatures.OrganizationDomains;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationDomains;

[SutProviderCustomize]
public class GetOrganizationDomainByIdQueryTests
{
    [Theory, BitAutoData]
    public async Task GetOrganizationDomainById_CallsGetByIdAsync(Guid id,
        SutProvider<GetOrganizationDomainByIdQuery> sutProvider)
    {
        await sutProvider.Sut.GetOrganizationDomainById(id);

        await sutProvider.GetDependency<IOrganizationDomainRepository>().Received(1)
            .GetByIdAsync(id);
    }
}
