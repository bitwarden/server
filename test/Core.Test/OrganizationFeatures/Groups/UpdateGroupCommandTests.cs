using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.OrganizationFeatures.Groups;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.AutoFixture.OrganizationFixtures;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.Groups;

[SutProviderCustomize]
public class UpdateGroupCommandTests
{
    [Theory, OrganizationCustomize(UseGroups = true), BitAutoData]
    public async Task UpdateGroup_Success(SutProvider<UpdateGroupCommand> sutProvider, Group group)
    {
        await sutProvider.Sut.UpdateGroupAsync(group);

        await sutProvider.GetDependency<IGroupRepository>().Received(1).ReplaceAsync(group);
        await sutProvider.GetDependency<IEventService>().Received(1).LogGroupEventAsync(group, Enums.EventType.Group_Updated);
        AssertHelper.AssertRecent(group.RevisionDate);
    }

    [Theory, OrganizationCustomize(UseGroups = true), BitAutoData]
    public async Task UpdateGroup_WithCollections_Success(SutProvider<UpdateGroupCommand> sutProvider, Group group, List<SelectionReadOnly> collections)
    {
        await sutProvider.Sut.UpdateGroupAsync(group, collections);

        await sutProvider.GetDependency<IGroupRepository>().Received(1).ReplaceAsync(group, collections);
        await sutProvider.GetDependency<IEventService>().Received(1).LogGroupEventAsync(group, Enums.EventType.Group_Updated);
        AssertHelper.AssertRecent(group.RevisionDate);
    }

    [Theory, OrganizationCustomize(UseGroups = true), BitAutoData]
    public async Task UpdateGroup_WithEventSystemUser_Success(SutProvider<UpdateGroupCommand> sutProvider, Group group, EventSystemUser eventSystemUser)
    {
        await sutProvider.Sut.UpdateGroupAsync(group, eventSystemUser);

        await sutProvider.GetDependency<IGroupRepository>().Received(1).ReplaceAsync(group);
        await sutProvider.GetDependency<IEventService>().Received(1).LogGroupEventAsync(group, Enums.EventType.Group_Updated, eventSystemUser);
        AssertHelper.AssertRecent(group.RevisionDate);
    }

    [Theory, OrganizationCustomize(UseGroups = false), BitAutoData]
    public void Validate_WithNullOrganization_ThrowsBadRequest(SutProvider<UpdateGroupCommand> sutProvider)
    {
        var exception = Assert.Throws<BadRequestException>(() => sutProvider.Sut.Validate(null));

        Assert.Contains("Organization not found", exception.Message);
    }

    [Theory, OrganizationCustomize(UseGroups = false), BitAutoData]
    public void Validate_WithUseGroupsAsFalse_ThrowsBadRequest(SutProvider<UpdateGroupCommand> sutProvider, Organization organization)
    {
        var exception = Assert.Throws<BadRequestException>(() => sutProvider.Sut.Validate(organization));

        Assert.Contains("This organization cannot use groups", exception.Message);
    }
}
