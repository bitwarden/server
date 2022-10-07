using Bit.Core.Commands;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Queries.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Commands.Users;

[SutProviderCustomize]
public class DeleteOrganizationUserCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task DeleteUser_Success(SutProvider<DeleteOrganizationUserCommand> sutProvider, Guid organizationId, Guid organizationUserId)
    {
        var organizationUser = new OrganizationUser { Id = organizationUserId, OrganizationId = organizationId };

        sutProvider.GetDependency<IOrganizationHasConfirmedOwnersExceptQuery>()
            .HasConfirmedOwnersExceptAsync(organizationId, Arg.Any<IEnumerable<Guid>>())
            .Returns(true);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUserId)
            .Returns(organizationUser);

        await sutProvider.Sut.DeleteUserAsync(organizationId, organizationUserId, null);

        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1).GetByIdAsync(organizationUserId);
        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1).DeleteAsync(organizationUser);
        await sutProvider.GetDependency<IEventService>().Received(1).LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_Removed);
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteUser_NotFound_Throws(SutProvider<DeleteOrganizationUserCommand> sutProvider, Guid organizationId, Guid organizationUserId)
    {
        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.DeleteUserAsync(organizationId, organizationUserId, null));
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteUser_MismatchingOrganizationId_Throws(SutProvider<DeleteOrganizationUserCommand> sutProvider, Guid organizationId, Guid organizationUserId)
    {
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUserId)
            .Returns(new OrganizationUser
            {
                Id = organizationUserId,
                OrganizationId = Guid.NewGuid()
            });

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.DeleteUserAsync(organizationId, organizationUserId, null));
    }
}
