using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data.Organizations.OrganizationConnections;
using Bit.Core.Models.OrganizationConnectionConfigs;
using Bit.Core.OrganizationFeatures.OrganizationConnections;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationConnections;

[SutProviderCustomize]
public class UpdateOrganizationConnectionCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task UpdateAsync_NoId_Fails(OrganizationConnectionData<BillingSyncConfig> data,
        SutProvider<UpdateOrganizationConnectionCommand> sutProvider)
    {
        data.Id = null;

        var exception = await Assert.ThrowsAsync<Exception>(() => sutProvider.Sut.UpdateAsync(data));

        Assert.Contains("Cannot update connection, Connection does not exist.", exception.Message);
        await sutProvider.GetDependency<IOrganizationConnectionRepository>().DidNotReceiveWithAnyArgs()
            .UpsertAsync(default);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateAsync_ConnectionDoesNotExist_ThrowsNotFound(
        OrganizationConnectionData<BillingSyncConfig> data,
        SutProvider<UpdateOrganizationConnectionCommand> sutProvider)
    {
        var exception = await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.UpdateAsync(data));

        await sutProvider.GetDependency<IOrganizationConnectionRepository>().DidNotReceiveWithAnyArgs()
            .UpsertAsync(default);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateAsync_CallsUpsert(OrganizationConnectionData<BillingSyncConfig> data,
        OrganizationConnection existing,
        SutProvider<UpdateOrganizationConnectionCommand> sutProvider)
    {
        data.Id = existing.Id;

        sutProvider.GetDependency<IOrganizationConnectionRepository>().GetByIdAsync(data.Id.Value).Returns(existing);
        await sutProvider.Sut.UpdateAsync(data);

        await sutProvider.GetDependency<IOrganizationConnectionRepository>().Received(1)
            .UpsertAsync(Arg.Is(AssertHelper.AssertPropertyEqual(data.ToEntity())));
    }
}
