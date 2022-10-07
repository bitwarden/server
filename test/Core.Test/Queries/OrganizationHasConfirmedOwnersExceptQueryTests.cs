using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Queries;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Queries;

[SutProviderCustomize]
public class OrganizationHasConfirmedOwnersExceptQueryTests
{
    [Theory]
    [BitAutoData]
    public async Task HasConfirmedOwnersExcept_ConfirmedUser_ReturnsTrue(SutProvider<OrganizationHasConfirmedOwnersExceptQuery> sutProvider, Guid organizationId, IEnumerable<Guid> organizationUsersId)
    {
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByOrganizationAsync(organizationId, OrganizationUserType.Owner)
            .Returns(new List<OrganizationUser>
            {
                new OrganizationUser { Id = Guid.NewGuid(), OrganizationId = organizationId, Status = OrganizationUserStatusType.Confirmed }
            });

        var result = await sutProvider.Sut.HasConfirmedOwnersExceptAsync(organizationId, organizationUsersId);

        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1).GetManyByOrganizationAsync(organizationId, OrganizationUserType.Owner);
        await sutProvider.GetDependency<ICurrentContext>().Received(0).ProviderIdForOrg(organizationId);

        Assert.True(result);
    }

    [Theory]
    [BitAutoData]
    public async Task HasConfirmedOwnersExcept_ConfirmedUser_ReturnsFalse(SutProvider<OrganizationHasConfirmedOwnersExceptQuery> sutProvider, Guid organizationId, IEnumerable<Guid> organizationUsersId)
    {
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByOrganizationAsync(organizationId, OrganizationUserType.Owner)
            .Returns(new List<OrganizationUser>
            {
                new OrganizationUser { Id = organizationUsersId.First(), OrganizationId = organizationId, Status = OrganizationUserStatusType.Confirmed }
            });

        var result = await sutProvider.Sut.HasConfirmedOwnersExceptAsync(organizationId, organizationUsersId);

        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1).GetManyByOrganizationAsync(organizationId, OrganizationUserType.Owner);
        await sutProvider.GetDependency<ICurrentContext>().Received(1).ProviderIdForOrg(organizationId);

        Assert.False(result);
    }

    [Theory]
    [BitAutoData]
    public async Task HasConfirmedOwnersExcept_ZeroConfirmedUsers_ReturnsFalse(SutProvider<OrganizationHasConfirmedOwnersExceptQuery> sutProvider, Guid organizationId, IEnumerable<Guid> organizationUsersId)
    {
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByOrganizationAsync(organizationId, OrganizationUserType.Owner)
            .Returns(new List<OrganizationUser>
            {
                new OrganizationUser { Id = Guid.NewGuid(), OrganizationId = organizationId, Status = OrganizationUserStatusType.Invited }
            });

        var result = await sutProvider.Sut.HasConfirmedOwnersExceptAsync(organizationId, organizationUsersId);

        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1).GetManyByOrganizationAsync(organizationId, OrganizationUserType.Owner);
        await sutProvider.GetDependency<ICurrentContext>().Received(1).ProviderIdForOrg(organizationId);

        Assert.False(result);
    }
}
