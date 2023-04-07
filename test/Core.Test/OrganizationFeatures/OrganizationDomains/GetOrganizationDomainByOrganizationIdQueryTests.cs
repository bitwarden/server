using Bit.Core.OrganizationFeatures.OrganizationDomains;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationDomains;

[SutProviderCustomize]
public class GetOrganizationDomainByOrganizationIdQueryTests
{
    [Theory, BitAutoData]
    public async Task GetDomainsByOrganizationId_CallsGetDomainsByOrganizationIdAsync(Guid orgId,
        SutProvider<GetOrganizationDomainByOrganizationIdQuery> sutProvider)
    {
        await sutProvider.Sut.GetDomainsByOrganizationId(orgId);

        await sutProvider.GetDependency<IOrganizationDomainRepository>().Received(1)
            .GetDomainsByOrganizationIdAsync(orgId);
    }
}
