using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationConnections;
using Bit.Core.Models.Data.Organizations.OrganizationConnections;
using Bit.Core.Models.OrganizationConnectionConfigs;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationConnections;

[SutProviderCustomize]
public class CreateOrganizationConnectionCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task CreateAsync_CallsCreate(
        OrganizationConnectionData<BillingSyncConfig> data,
        SutProvider<CreateOrganizationConnectionCommand> sutProvider
    )
    {
        await sutProvider.Sut.CreateAsync(data);

        await sutProvider
            .GetDependency<IOrganizationConnectionRepository>()
            .Received(1)
            .CreateAsync(Arg.Is(AssertHelper.AssertPropertyEqual(data.ToEntity())));
    }
}
