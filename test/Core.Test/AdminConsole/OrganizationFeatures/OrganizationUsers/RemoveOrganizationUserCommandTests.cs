using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers;

[SutProviderCustomize]
public class RemoveOrganizationUserCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task RemoveUser_Success(SutProvider<RemoveOrganizationUserCommand> sutProvider, Guid organizationId, Guid organizationUserId)
    {
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUserId)
            .Returns(new OrganizationUser
            {
                Id = organizationUserId,
                OrganizationId = organizationId
            });

        await sutProvider.Sut.RemoveUserAsync(organizationId, organizationUserId, null);

        await sutProvider.GetDependency<IOrganizationService>().Received(1).RemoveUserAsync(organizationId, organizationUserId, null);
    }

    [Theory]
    [BitAutoData]
    public async Task RemoveUser_NotFound_Throws(SutProvider<RemoveOrganizationUserCommand> sutProvider, Guid organizationId, Guid organizationUserId)
    {
        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.RemoveUserAsync(organizationId, organizationUserId, null));
    }

    [Theory]
    [BitAutoData]
    public async Task RemoveUser_MismatchingOrganizationId_Throws(SutProvider<RemoveOrganizationUserCommand> sutProvider, Guid organizationId, Guid organizationUserId)
    {
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUserId)
            .Returns(new OrganizationUser
            {
                Id = organizationUserId,
                OrganizationId = Guid.NewGuid()
            });

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.RemoveUserAsync(organizationId, organizationUserId, null));
    }

    [Theory]
    [BitAutoData]
    public async Task RemoveUser_WithEventSystemUser_Success(SutProvider<RemoveOrganizationUserCommand> sutProvider, Guid organizationId, Guid organizationUserId, EventSystemUser eventSystemUser)
    {
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUserId)
            .Returns(new OrganizationUser
            {
                Id = organizationUserId,
                OrganizationId = organizationId
            });

        await sutProvider.Sut.RemoveUserAsync(organizationId, organizationUserId, eventSystemUser);

        await sutProvider.GetDependency<IOrganizationService>().Received(1).RemoveUserAsync(organizationId, organizationUserId, eventSystemUser);
    }
}
