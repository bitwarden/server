using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.AutoFixture.OrganizationUserFixtures;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationUsers;

[SutProviderCustomize]
public class UpdateOrganizationUserGroupsCommandTests
{
    [Theory, BitAutoData]
    public async Task UpdateUserGroups_Passes(
        OrganizationUser organizationUser,
        IEnumerable<Guid> groupIds,
        SutProvider<UpdateOrganizationUserGroupsCommand> sutProvider)
    {
        await sutProvider.Sut.UpdateUserGroupsAsync(organizationUser, groupIds, null);

        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1)
            .UpdateGroupsAsync(organizationUser.Id, groupIds);
        await sutProvider.GetDependency<IEventService>().Received(1)
            .LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_UpdatedGroups);
    }

    [Theory, BitAutoData]
    public async Task UpdateUserGroups_WithSavingUserId_Passes(
        OrganizationUser organizationUser,
        IEnumerable<Guid> groupIds,
        Guid savingUserId,
        SutProvider<UpdateOrganizationUserGroupsCommand> sutProvider)
    {
        organizationUser.Permissions = null;

        var currentContext = sutProvider.GetDependency<ICurrentContext>();
        currentContext.OrganizationOwner(organizationUser.OrganizationId).Returns(true);

        await sutProvider.Sut.UpdateUserGroupsAsync(organizationUser, groupIds, savingUserId);

        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1)
            .UpdateGroupsAsync(organizationUser.Id, groupIds);
        await sutProvider.GetDependency<IEventService>().Received(1)
            .LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_UpdatedGroups);
    }

    [Theory, BitAutoData]
    public async Task UpdateUserGroups_WithSavingUserId_WithNoPermission_Throws(
        [OrganizationUser(type: OrganizationUserType.Custom)] OrganizationUser organizationUser,
        IEnumerable<Guid> groupIds,
        Guid savingUserId,
        SutProvider<UpdateOrganizationUserGroupsCommand> sutProvider)
    {
        organizationUser.Permissions = null;

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateUserGroupsAsync(organizationUser, groupIds, savingUserId));
        Assert.Contains("your account does not have permission to manage users.", exception.Message.ToLowerInvariant());

        await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs()
            .UpdateGroupsAsync(default, default);
        await sutProvider.GetDependency<IEventService>().DidNotReceiveWithAnyArgs()
            .LogOrganizationUserEventAsync(default, default);
    }
}
