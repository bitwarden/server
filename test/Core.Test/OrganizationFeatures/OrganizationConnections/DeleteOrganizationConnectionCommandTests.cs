using Bit.Core.Entities;
using Bit.Core.OrganizationFeatures.OrganizationConnections;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationConnections;

[SutProviderCustomize]
public class DeleteOrganizationConnectionCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task DeleteAsync_CallsDelete(OrganizationConnection connection,
        SutProvider<DeleteOrganizationConnectionCommand> sutProvider)
    {
        await sutProvider.Sut.DeleteAsync(connection);

        await sutProvider.GetDependency<IOrganizationConnectionRepository>().Received(1)
            .DeleteAsync(connection);
    }
}
