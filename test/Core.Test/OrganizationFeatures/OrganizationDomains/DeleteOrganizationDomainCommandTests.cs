using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.OrganizationDomains;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationDomains;

[SutProviderCustomize]
public class DeleteOrganizationDomainCommandTests
{
    [Theory, BitAutoData]
    public async Task DeleteAsync_ShouldThrowNotFoundException_WhenIdDoesNotExist(Guid id,
        SutProvider<DeleteOrganizationDomainCommand> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationDomainRepository>().GetByIdAsync(id).ReturnsNull();

        var requestAction = async () => await sutProvider.Sut.DeleteAsync(id);

        await Assert.ThrowsAsync<NotFoundException>(requestAction);
    }

    [Theory, BitAutoData]
    public async Task DeleteAsync_Success(Guid id, SutProvider<DeleteOrganizationDomainCommand> sutProvider)
    {
        var expected = new OrganizationDomain
        {
            Id = id,
            OrganizationId = Guid.NewGuid(),
            DomainName = "Test Domain",
            Txt = "btw+test18383838383"
        };
        sutProvider.GetDependency<IOrganizationDomainRepository>().GetByIdAsync(id).Returns(expected);

        await sutProvider.Sut.DeleteAsync(id);

        await sutProvider.GetDependency<IOrganizationDomainRepository>().Received(1).DeleteAsync(expected);
        await sutProvider.GetDependency<IEventService>().Received(1)
            .LogOrganizationDomainEventAsync(Arg.Any<OrganizationDomain>(), EventType.OrganizationDomain_Removed);
    }
}
