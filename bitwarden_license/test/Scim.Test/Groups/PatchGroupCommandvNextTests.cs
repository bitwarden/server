using System.Text.Json;
using AutoFixture;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.Groups.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Scim.Groups;
using Bit.Scim.Models;
using Bit.Scim.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Scim.Test.Groups;

[SutProviderCustomize]
public class PatchGroupCommandvNextTests
{
    [Theory]
    [BitAutoData]
    public async Task PatchGroup_ReplaceListMembers_Success(SutProvider<PatchGroupCommandvNext> sutProvider,
        Organization organization, Group group, IEnumerable<Guid> userIds)
    {
        group.OrganizationId = organization.Id;

        var scimPatchModel = new ScimPatchModel
        {
            Operations = new List<ScimPatchModel.OperationModel>
            {
                new()
                {
                    Op = "replace",
                    Path = "members",
                    Value = JsonDocument.Parse(JsonSerializer.Serialize(userIds.Select(uid => new { value = uid }).ToArray())).RootElement
                }
            },
            Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
        };

        await sutProvider.Sut.PatchGroupAsync(group, scimPatchModel);

        await sutProvider.GetDependency<IGroupRepository>().Received(1).UpdateUsersAsync(
            group.Id,
            Arg.Is<IEnumerable<Guid>>(arg =>
                arg.Count() == userIds.Count() &&
                arg.ToHashSet().SetEquals(userIds)));
    }

    [Theory]
    [BitAutoData]
    public async Task PatchGroup_ReplaceDisplayNameFromPath_Success(
        SutProvider<PatchGroupCommandvNext> sutProvider, Organization organization, Group group, string displayName)
    {
        group.OrganizationId = organization.Id;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        var scimPatchModel = new ScimPatchModel
        {
            Operations = new List<ScimPatchModel.OperationModel>
            {
                new()
                {
                    Op = "replace",
                    Path = "displayname",
                    Value = JsonDocument.Parse($"\"{displayName}\"").RootElement
                }
            },
            Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
        };

        await sutProvider.Sut.PatchGroupAsync(group, scimPatchModel);

        await sutProvider.GetDependency<IUpdateGroupCommand>().Received(1).UpdateGroupAsync(group, organization, EventSystemUser.SCIM);
        Assert.Equal(displayName, group.Name);
    }

    [Theory]
    [BitAutoData]
    public async Task PatchGroup_ReplaceDisplayNameFromValueObject_Success(SutProvider<PatchGroupCommandvNext> sutProvider, Organization organization, Group group, string displayName)
    {
        group.OrganizationId = organization.Id;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        var scimPatchModel = new ScimPatchModel
        {
            Operations = new List<ScimPatchModel.OperationModel>
            {
                new()
                {
                    Op = "replace",
                    Value = JsonDocument.Parse($"{{\"displayName\":\"{displayName}\"}}").RootElement
                }
            },
            Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
        };

        await sutProvider.Sut.PatchGroupAsync(group, scimPatchModel);

        await sutProvider.GetDependency<IUpdateGroupCommand>().Received(1).UpdateGroupAsync(group, organization, EventSystemUser.SCIM);
        Assert.Equal(displayName, group.Name);
    }

    [Theory]
    [BitAutoData]
    public async Task PatchGroup_AddSingleMember_Success(SutProvider<PatchGroupCommandvNext> sutProvider, Organization organization, Group group, ICollection<Guid> existingMembers, Guid userId)
    {
        group.OrganizationId = organization.Id;

        sutProvider.GetDependency<IGroupRepository>()
            .GetManyUserIdsByIdAsync(group.Id, true)
            .Returns(existingMembers);

        var scimPatchModel = new ScimPatchModel
        {
            Operations = new List<ScimPatchModel.OperationModel>
            {
                new()
                {
                    Op = "add",
                    Path = $"members[value eq \"{userId}\"]",
                }
            },
            Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
        };

        await sutProvider.Sut.PatchGroupAsync(group, scimPatchModel);

        await sutProvider.GetDependency<IGroupRepository>().Received(1).AddGroupUsersByIdAsync(
            group.Id,
            Arg.Is<IEnumerable<Guid>>(arg => arg.Single() == userId));
    }

    [Theory]
    [BitAutoData]
    public async Task PatchGroup_AddSingleMember_ReturnsEarlyIfAlreadyInGroup(
        SutProvider<PatchGroupCommandvNext> sutProvider,
        Organization organization,
        Group group,
        ICollection<Guid> existingMembers)
    {
        // User being added is already in group
        var userId = existingMembers.First();
        group.OrganizationId = organization.Id;

        sutProvider.GetDependency<IGroupRepository>()
            .GetManyUserIdsByIdAsync(group.Id, true)
            .Returns(existingMembers);

        var scimPatchModel = new ScimPatchModel
        {
            Operations = new List<ScimPatchModel.OperationModel>
            {
                new()
                {
                    Op = "add",
                    Path = $"members[value eq \"{userId}\"]",
                }
            },
            Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
        };

        await sutProvider.Sut.PatchGroupAsync(group, scimPatchModel);

        await sutProvider.GetDependency<IGroupRepository>()
            .DidNotReceiveWithAnyArgs()
            .AddGroupUsersByIdAsync(default, default);
    }

    [Theory]
    [BitAutoData]
    public async Task PatchGroup_AddListMembers_Success(SutProvider<PatchGroupCommandvNext> sutProvider, Organization organization, Group group, ICollection<Guid> existingMembers, ICollection<Guid> userIds)
    {
        group.OrganizationId = organization.Id;

        sutProvider.GetDependency<IGroupRepository>()
            .GetManyUserIdsByIdAsync(group.Id, true)
            .Returns(existingMembers);

        var scimPatchModel = new ScimPatchModel
        {
            Operations = new List<ScimPatchModel.OperationModel>
            {
                new()
                {
                    Op = "add",
                    Path = $"members",
                    Value = JsonDocument.Parse(JsonSerializer.Serialize(userIds.Select(uid => new { value = uid }).ToArray())).RootElement
                }
            },
            Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
        };

        await sutProvider.Sut.PatchGroupAsync(group, scimPatchModel);

        await sutProvider.GetDependency<IGroupRepository>().Received(1).AddGroupUsersByIdAsync(
            group.Id,
            Arg.Is<IEnumerable<Guid>>(arg =>
                arg.Count() == userIds.Count &&
                arg.ToHashSet().SetEquals(userIds)));
    }

    [Theory]
    [BitAutoData]
    public async Task PatchGroup_AddListMembers_IgnoresDuplicatesInRequest(
        SutProvider<PatchGroupCommandvNext> sutProvider, Organization organization, Group group,
        ICollection<Guid> existingMembers)
    {
        // Create 3 userIds
        var fixture = new Fixture { RepeatCount = 3 };
        var userIds = fixture.CreateMany<Guid>().ToList();

        // Copy the list and add a duplicate
        var userIdsWithDuplicate = userIds.Append(userIds.First()).ToList();
        Assert.Equal(4, userIdsWithDuplicate.Count);

        group.OrganizationId = organization.Id;

        sutProvider.GetDependency<IGroupRepository>()
            .GetManyUserIdsByIdAsync(group.Id, true)
            .Returns(existingMembers);

        var scimPatchModel = new ScimPatchModel
        {
            Operations = new List<ScimPatchModel.OperationModel>
            {
                new()
                {
                    Op = "add",
                    Path = $"members",
                    Value = JsonDocument.Parse(JsonSerializer
                        .Serialize(userIdsWithDuplicate
                            .Select(uid => new { value = uid })
                            .ToArray())).RootElement
                }
            },
            Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
        };

        await sutProvider.Sut.PatchGroupAsync(group, scimPatchModel);

        await sutProvider.GetDependency<IGroupRepository>().Received(1).AddGroupUsersByIdAsync(
            group.Id,
            Arg.Is<IEnumerable<Guid>>(arg =>
                arg.Count() == 3 &&
                arg.ToHashSet().SetEquals(userIds)));
    }

    [Theory]
    [BitAutoData]
    public async Task PatchGroup_AddListMembers_SuccessIfOnlySomeUsersAreInGroup(
        SutProvider<PatchGroupCommandvNext> sutProvider,
        Organization organization, Group group,
        ICollection<Guid> existingMembers,
        ICollection<Guid> userIds)
    {
        // A user is already in the group, but some still need to be added
        userIds.Add(existingMembers.First());

        group.OrganizationId = organization.Id;

        sutProvider.GetDependency<IGroupRepository>()
            .GetManyUserIdsByIdAsync(group.Id, true)
            .Returns(existingMembers);

        var scimPatchModel = new ScimPatchModel
        {
            Operations = new List<ScimPatchModel.OperationModel>
            {
                new()
                {
                    Op = "add",
                    Path = $"members",
                    Value = JsonDocument.Parse(JsonSerializer.Serialize(userIds.Select(uid => new { value = uid }).ToArray())).RootElement
                }
            },
            Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
        };

        await sutProvider.Sut.PatchGroupAsync(group, scimPatchModel);

        await sutProvider.GetDependency<IGroupRepository>()
            .Received(1)
            .AddGroupUsersByIdAsync(
                group.Id,
                Arg.Is<IEnumerable<Guid>>(arg =>
                    arg.Count() == userIds.Count &&
                    arg.ToHashSet().SetEquals(userIds)));
    }

    [Theory]
    [BitAutoData]
    public async Task PatchGroup_RemoveSingleMember_Success(SutProvider<PatchGroupCommandvNext> sutProvider, Organization organization, Group group, Guid userId)
    {
        group.OrganizationId = organization.Id;

        var scimPatchModel = new Models.ScimPatchModel
        {
            Operations = new List<ScimPatchModel.OperationModel>
            {
                new ScimPatchModel.OperationModel
                {
                    Op = "remove",
                    Path = $"members[value eq \"{userId}\"]",
                }
            },
            Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
        };

        await sutProvider.Sut.PatchGroupAsync(group, scimPatchModel);

        await sutProvider.GetDependency<IGroupService>().Received(1).DeleteUserAsync(group, userId, EventSystemUser.SCIM);
    }

    [Theory]
    [BitAutoData]
    public async Task PatchGroup_RemoveListMembers_Success(SutProvider<PatchGroupCommandvNext> sutProvider,
        Organization organization, Group group, ICollection<Guid> existingMembers)
    {
        List<Guid> usersToRemove = [existingMembers.First(), existingMembers.Skip(1).First()];
        group.OrganizationId = organization.Id;

        sutProvider.GetDependency<IGroupRepository>()
            .GetManyUserIdsByIdAsync(group.Id)
            .Returns(existingMembers);

        var scimPatchModel = new Models.ScimPatchModel
        {
            Operations = new List<ScimPatchModel.OperationModel>
            {
                new()
                {
                    Op = "remove",
                    Path = $"members",
                    Value = JsonDocument.Parse(JsonSerializer.Serialize(usersToRemove.Select(uid => new { value = uid }).ToArray())).RootElement
                }
            },
            Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
        };

        await sutProvider.Sut.PatchGroupAsync(group, scimPatchModel);

        var expectedRemainingUsers = existingMembers.Skip(2).ToList();
        await sutProvider.GetDependency<IGroupRepository>()
            .Received(1)
            .UpdateUsersAsync(
                group.Id,
                Arg.Is<IEnumerable<Guid>>(arg =>
                    arg.Count() == expectedRemainingUsers.Count &&
                    arg.ToHashSet().SetEquals(expectedRemainingUsers)));
    }

    [Theory]
    [BitAutoData]
    public async Task PatchGroup_NoAction_Success(
        SutProvider<PatchGroupCommandvNext> sutProvider, Organization organization, Group group)
    {
        group.OrganizationId = organization.Id;

        var scimPatchModel = new Models.ScimPatchModel
        {
            Operations = new List<ScimPatchModel.OperationModel>(),
            Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
        };

        await sutProvider.Sut.PatchGroupAsync(group, scimPatchModel);

        await sutProvider.GetDependency<IGroupRepository>().DidNotReceiveWithAnyArgs().UpdateUsersAsync(default, default);
        await sutProvider.GetDependency<IGroupRepository>().DidNotReceiveWithAnyArgs().GetManyUserIdsByIdAsync(default);
        await sutProvider.GetDependency<IUpdateGroupCommand>().DidNotReceiveWithAnyArgs().UpdateGroupAsync(default, default);
        await sutProvider.GetDependency<IGroupService>().DidNotReceiveWithAnyArgs().DeleteUserAsync(default, default);
    }
}
