using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationDomains;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationDomains;

[SutProviderCustomize]
public class GetOrganizationDomainByOrganizationIdQueryTests
{
    [Theory, BitAutoData]
    public async Task GetDomainsByOrganizationId_CallsGetDomainsByOrganizationIdAsync(
        Guid orgId,
        SutProvider<GetOrganizationDomainByOrganizationIdQuery> sutProvider
    )
    {
        await sutProvider.Sut.GetDomainsByOrganizationIdAsync(orgId);

        await sutProvider
            .GetDependency<IOrganizationDomainRepository>()
            .Received(1)
            .GetDomainsByOrganizationIdAsync(orgId);
    }
}
