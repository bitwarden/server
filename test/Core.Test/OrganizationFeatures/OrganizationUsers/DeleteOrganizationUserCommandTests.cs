using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationUsers;

[SutProviderCustomize]
public class DeleteOrganizationUserCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task DeleteUser_Success(SutProvider<DeleteOrganizationUserCommand> sutProvider, Guid organizationId, Guid organizationUserId)
    {
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUserId)
            .Returns(new OrganizationUser
            {
                Id = organizationUserId,
                OrganizationId = organizationId
            });

        await sutProvider.Sut.DeleteUserAsync(organizationId, organizationUserId, null);

        await sutProvider.GetDependency<IOrganizationService>().Received(1).DeleteUserAsync(organizationId, organizationUserId, null);
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

    [Theory]
    [BitAutoData]
    public async Task DeleteUser_WithEventSystemUser_Success(SutProvider<DeleteOrganizationUserCommand> sutProvider, Guid organizationId, Guid organizationUserId, EventSystemUser eventSystemUser)
    {
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUserId)
            .Returns(new OrganizationUser
            {
                Id = organizationUserId,
                OrganizationId = organizationId
            });

        await sutProvider.Sut.DeleteUserAsync(organizationId, organizationUserId, eventSystemUser);

        await sutProvider.GetDependency<IOrganizationService>().Received(1).DeleteUserAsync(organizationId, organizationUserId, eventSystemUser);
    }
}
