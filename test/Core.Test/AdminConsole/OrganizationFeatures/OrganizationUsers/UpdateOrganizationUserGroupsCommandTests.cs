using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationUserAction;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers;

[SutProviderCustomize]
public class UpdateOrganizationUserGroupsCommandTests
{
    private static readonly DateTime _expectedRevisionDate = DateTime.UtcNow.AddYears(1);

    [Theory, BitAutoData]
    public async Task UpdateUserGroups_ShouldUpdateUserGroupsAndLogUserEvent(
        OrganizationUser organizationUser,
        IEnumerable<Guid> groupIds)
    {
        var sutProvider = new SutProvider<UpdateOrganizationUserGroupsCommand>()
            .WithFakeTimeProvider()
            .Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_expectedRevisionDate);

        await sutProvider.Sut.UpdateUserGroupsAsync(organizationUser, groupIds);

        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1)
            .UpdateGroupsAsync(organizationUser.Id, groupIds, Arg.Is<DateTime>(d => d == _expectedRevisionDate));
        await sutProvider.GetDependency<IEventService>().Received(1)
            .LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_UpdatedGroups);
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.Custom)]
    public async Task UpdateUserGroups_WhenScopedApiKeyTargetsElevatedMember_Throws(
        OrganizationUserType type,
        OrganizationUser organizationUser,
        IEnumerable<Guid> groupIds)
    {
        var sutProvider = CreateSutProvider(isScopedApiKey: true);
        organizationUser.Type = type;

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateUserGroupsAsync(organizationUser, groupIds));

        Assert.Equal(new ScopedApiKeyCanOnlyManageUsers().Message, exception.Message);
        await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs()
            .UpdateGroupsAsync(default, default!, default);
    }

    [Theory, BitAutoData]
    public async Task UpdateUserGroups_WhenScopedApiKeyTargetsUser_UpdatesGroups(
        OrganizationUser organizationUser,
        IEnumerable<Guid> groupIds)
    {
        var sutProvider = CreateSutProvider(isScopedApiKey: true);
        organizationUser.Type = OrganizationUserType.User;

        await sutProvider.Sut.UpdateUserGroupsAsync(organizationUser, groupIds);

        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1)
            .UpdateGroupsAsync(organizationUser.Id, groupIds, Arg.Any<DateTime>());
    }

    [Theory, BitAutoData]
    public async Task UpdateUserGroups_WhenNotScopedApiKeyTargetsAdmin_UpdatesGroups(
        OrganizationUser organizationUser,
        IEnumerable<Guid> groupIds)
    {
        var sutProvider = CreateSutProvider(isScopedApiKey: false);
        organizationUser.Type = OrganizationUserType.Admin;

        await sutProvider.Sut.UpdateUserGroupsAsync(organizationUser, groupIds);

        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1)
            .UpdateGroupsAsync(organizationUser.Id, groupIds, Arg.Any<DateTime>());
    }

    private static SutProvider<UpdateOrganizationUserGroupsCommand> CreateSutProvider(bool isScopedApiKey)
    {
        var sutProvider = new SutProvider<UpdateOrganizationUserGroupsCommand>()
            .WithFakeTimeProvider()
            .Create();
        sutProvider.GetDependency<ICurrentContext>().IsScopedOrganizationApiKey.Returns(isScopedApiKey);
        return sutProvider;
    }
}
