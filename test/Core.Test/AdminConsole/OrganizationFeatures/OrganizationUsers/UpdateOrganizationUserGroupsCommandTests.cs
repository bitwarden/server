using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;
using Bit.Core.Entities;
using Bit.Core.Enums;
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
}
