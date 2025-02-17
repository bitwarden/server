﻿using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.Groups.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Scim.Groups;
using Bit.Scim.Models;
using Bit.Scim.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using NSubstitute;
using Xunit;

namespace Bit.Scim.Test.Groups;

[SutProviderCustomize]
public class PostGroupCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task PostGroup_Success(SutProvider<PostGroupCommand> sutProvider, string displayName, string externalId, Organization organization, ICollection<Group> groups)
    {
        var scimGroupRequestModel = new ScimGroupRequestModel
        {
            DisplayName = displayName,
            ExternalId = externalId,
            Members = new List<ScimGroupRequestModel.GroupMembersModel>(),
            Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
        };

        var expectedResult = new Group
        {
            OrganizationId = organization.Id,
            Name = displayName,
            ExternalId = externalId,
        };

        sutProvider.GetDependency<IGroupRepository>()
            .GetManyByOrganizationIdAsync(organization.Id)
            .Returns(groups);

        var group = await sutProvider.Sut.PostGroupAsync(organization, scimGroupRequestModel);

        await sutProvider.GetDependency<ICreateGroupCommand>().Received(1).CreateGroupAsync(group, organization, EventSystemUser.SCIM, null);
        await sutProvider.GetDependency<IGroupRepository>().DidNotReceiveWithAnyArgs().UpdateUsersAsync(default, default);

        AssertHelper.AssertPropertyEqual(expectedResult, group, "Id", "CreationDate", "RevisionDate");
    }

    [Theory]
    [BitAutoData]
    public async Task PostGroup_WithMembers_Success(SutProvider<PostGroupCommand> sutProvider, string displayName, string externalId, Organization organization, ICollection<Group> groups, IEnumerable<Guid> membersUserIds)
    {
        var scimGroupRequestModel = new ScimGroupRequestModel
        {
            DisplayName = displayName,
            ExternalId = externalId,
            Members = membersUserIds.Select(uid => new ScimGroupRequestModel.GroupMembersModel { Value = uid.ToString() }).ToList(),
            Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
        };

        var expectedResult = new Group
        {
            OrganizationId = organization.Id,
            Name = displayName,
            ExternalId = externalId
        };

        sutProvider.GetDependency<IGroupRepository>()
            .GetManyByOrganizationIdAsync(organization.Id)
            .Returns(groups);

        var group = await sutProvider.Sut.PostGroupAsync(organization, scimGroupRequestModel);

        await sutProvider.GetDependency<ICreateGroupCommand>().Received(1).CreateGroupAsync(group, organization, EventSystemUser.SCIM, null);
        await sutProvider.GetDependency<IGroupRepository>().Received(1).UpdateUsersAsync(Arg.Any<Guid>(), Arg.Is<IEnumerable<Guid>>(arg => arg.All(id => membersUserIds.Contains(id))));

        AssertHelper.AssertPropertyEqual(expectedResult, group, "Id", "CreationDate", "RevisionDate");
    }

    [Theory]
    [BitAutoData((string)null)]
    [BitAutoData("")]
    [BitAutoData(" ")]
    public async Task PostGroup_NullDisplayName_Throws(string displayName, SutProvider<PostGroupCommand> sutProvider, Organization organization)
    {
        var scimGroupRequestModel = new ScimGroupRequestModel
        {
            DisplayName = displayName,
            ExternalId = Guid.NewGuid().ToString(),
            Members = new List<ScimGroupRequestModel.GroupMembersModel>(),
            Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
        };

        await Assert.ThrowsAsync<BadRequestException>(async () => await sutProvider.Sut.PostGroupAsync(organization, scimGroupRequestModel));
    }

    [Theory]
    [BitAutoData]
    public async Task PostGroup_ExistingExternalId_Throws(string displayName, SutProvider<PostGroupCommand> sutProvider, Organization organization, ICollection<Group> groups)
    {
        var scimGroupRequestModel = new ScimGroupRequestModel
        {
            DisplayName = displayName,
            ExternalId = groups.First().ExternalId,
            Members = new List<ScimGroupRequestModel.GroupMembersModel>(),
            Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
        };

        sutProvider.GetDependency<IGroupRepository>()
            .GetManyByOrganizationIdAsync(organization.Id)
            .Returns(groups);

        await Assert.ThrowsAsync<ConflictException>(async () => await sutProvider.Sut.PostGroupAsync(organization, scimGroupRequestModel));
    }
}
