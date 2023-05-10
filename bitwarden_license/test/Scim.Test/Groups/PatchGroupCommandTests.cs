using System.Text.Json;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.Groups.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Scim.Groups;
using Bit.Scim.Models;
using Bit.Scim.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Scim.Test.Groups;

[SutProviderCustomize]
public class PatchGroupCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task PatchGroup_ReplaceListMembers_Success(SutProvider<PatchGroupCommand> sutProvider, Organization organization, Group group, IEnumerable<Guid> userIds)
    {
        group.OrganizationId = organization.Id;

        sutProvider.GetDependency<IGroupRepository>()
            .GetByIdAsync(group.Id)
            .Returns(group);

        var scimPatchModel = new Models.ScimPatchModel
        {
            Operations = new List<ScimPatchModel.OperationModel>
            {
                new ScimPatchModel.OperationModel
                {
                    Op = "replace",
                    Path = "members",
                    Value = JsonDocument.Parse(JsonSerializer.Serialize(userIds.Select(uid => new { value = uid }).ToArray())).RootElement
                }
            },
            Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
        };

        await sutProvider.Sut.PatchGroupAsync(organization, group.Id, scimPatchModel);

        await sutProvider.GetDependency<IGroupRepository>().Received(1).UpdateUsersAsync(group.Id, Arg.Is<IEnumerable<Guid>>(arg => arg.All(id => userIds.Contains(id))));
    }

    [Theory]
    [BitAutoData]
    public async Task PatchGroup_ReplaceDisplayNameFromPath_Success(SutProvider<PatchGroupCommand> sutProvider, Organization organization, Group group, string displayName)
    {
        group.OrganizationId = organization.Id;

        sutProvider.GetDependency<IGroupRepository>()
            .GetByIdAsync(group.Id)
            .Returns(group);

        var scimPatchModel = new Models.ScimPatchModel
        {
            Operations = new List<ScimPatchModel.OperationModel>
            {
                new ScimPatchModel.OperationModel
                {
                    Op = "replace",
                    Path = "displayname",
                    Value = JsonDocument.Parse($"\"{displayName}\"").RootElement
                }
            },
            Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
        };

        await sutProvider.Sut.PatchGroupAsync(organization, group.Id, scimPatchModel);

        await sutProvider.GetDependency<IUpdateGroupCommand>().Received(1).UpdateGroupAsync(group, organization, EventSystemUser.SCIM);
        Assert.Equal(displayName, group.Name);
    }

    [Theory]
    [BitAutoData]
    public async Task PatchGroup_ReplaceDisplayNameFromValueObject_Success(SutProvider<PatchGroupCommand> sutProvider, Organization organization, Group group, string displayName)
    {
        group.OrganizationId = organization.Id;

        sutProvider.GetDependency<IGroupRepository>()
            .GetByIdAsync(group.Id)
            .Returns(group);

        var scimPatchModel = new Models.ScimPatchModel
        {
            Operations = new List<ScimPatchModel.OperationModel>
            {
                new ScimPatchModel.OperationModel
                {
                    Op = "replace",
                    Value = JsonDocument.Parse($"{{\"displayName\":\"{displayName}\"}}").RootElement
                }
            },
            Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
        };

        await sutProvider.Sut.PatchGroupAsync(organization, group.Id, scimPatchModel);

        await sutProvider.GetDependency<IUpdateGroupCommand>().Received(1).UpdateGroupAsync(group, organization, EventSystemUser.SCIM);
        Assert.Equal(displayName, group.Name);
    }

    [Theory]
    [BitAutoData]
    public async Task PatchGroup_AddSingleMember_Success(SutProvider<PatchGroupCommand> sutProvider, Organization organization, Group group, ICollection<Guid> existingMembers, Guid userId)
    {
        group.OrganizationId = organization.Id;

        sutProvider.GetDependency<IGroupRepository>()
            .GetByIdAsync(group.Id)
            .Returns(group);

        sutProvider.GetDependency<IGroupRepository>()
            .GetManyUserIdsByIdAsync(group.Id)
            .Returns(existingMembers);

        var scimPatchModel = new Models.ScimPatchModel
        {
            Operations = new List<ScimPatchModel.OperationModel>
            {
                new ScimPatchModel.OperationModel
                {
                    Op = "add",
                    Path = $"members[value eq \"{userId}\"]",
                }
            },
            Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
        };

        await sutProvider.Sut.PatchGroupAsync(organization, group.Id, scimPatchModel);

        await sutProvider.GetDependency<IGroupRepository>().Received(1).UpdateUsersAsync(group.Id, Arg.Is<IEnumerable<Guid>>(arg => arg.All(id => existingMembers.Append(userId).Contains(id))));
    }

    [Theory]
    [BitAutoData]
    public async Task PatchGroup_AddListMembers_Success(SutProvider<PatchGroupCommand> sutProvider, Organization organization, Group group, ICollection<Guid> existingMembers, ICollection<Guid> userIds)
    {
        group.OrganizationId = organization.Id;

        sutProvider.GetDependency<IGroupRepository>()
            .GetByIdAsync(group.Id)
            .Returns(group);

        sutProvider.GetDependency<IGroupRepository>()
            .GetManyUserIdsByIdAsync(group.Id)
            .Returns(existingMembers);

        var scimPatchModel = new Models.ScimPatchModel
        {
            Operations = new List<ScimPatchModel.OperationModel>
            {
                new ScimPatchModel.OperationModel
                {
                    Op = "add",
                    Path = $"members",
                    Value = JsonDocument.Parse(JsonSerializer.Serialize(userIds.Select(uid => new { value = uid }).ToArray())).RootElement
                }
            },
            Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
        };

        await sutProvider.Sut.PatchGroupAsync(organization, group.Id, scimPatchModel);

        await sutProvider.GetDependency<IGroupRepository>().Received(1).UpdateUsersAsync(group.Id, Arg.Is<IEnumerable<Guid>>(arg => arg.All(id => existingMembers.Concat(userIds).Contains(id))));
    }

    [Theory]
    [BitAutoData]
    public async Task PatchGroup_RemoveSingleMember_Success(SutProvider<PatchGroupCommand> sutProvider,
        Organization organization, Group group, OrganizationUser organizationUser)
    {
        group.OrganizationId = organization.Id;

        sutProvider.GetDependency<IGroupRepository>()
            .GetByIdAsync(group.Id)
            .Returns(group);

        sutProvider.GetDependency<IGroupRepository>()
            .GetGroupUserByGroupIdOrganizationUserId(group.Id, organizationUser.Id)
            .Returns(new GroupUser()
            {
                OrganizationUserId = organizationUser.Id,
                GroupId = group.Id
            });

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUser.Id)
            .Returns(organizationUser);

        var scimPatchModel = new Models.ScimPatchModel
        {
            Operations = new List<ScimPatchModel.OperationModel>
            {
                new ScimPatchModel.OperationModel
                {
                    Op = "remove",
                    Path = $"members[value eq \"{organizationUser.Id}\"]",
                }
            },
            Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
        };

        await sutProvider.Sut.PatchGroupAsync(organization, group.Id, scimPatchModel);

        await sutProvider.GetDependency<IGroupRepository>().Received(1).DeleteUserAsync(group.Id, organizationUser.Id);
        await sutProvider.GetDependency<IEventService>().Received(1).LogOrganizationUserEventAsync(organizationUser,
            EventType.OrganizationUser_UpdatedGroups, EventSystemUser.SCIM);
    }

    [Theory]
    [BitAutoData]
    public async Task PatchGroup_RemoveListMembers_Success(SutProvider<PatchGroupCommand> sutProvider, Organization organization, Group group, ICollection<Guid> existingMembers)
    {
        group.OrganizationId = organization.Id;

        sutProvider.GetDependency<IGroupRepository>()
            .GetByIdAsync(group.Id)
            .Returns(group);

        sutProvider.GetDependency<IGroupRepository>()
            .GetManyUserIdsByIdAsync(group.Id)
            .Returns(existingMembers);

        var scimPatchModel = new Models.ScimPatchModel
        {
            Operations = new List<ScimPatchModel.OperationModel>
            {
                new ScimPatchModel.OperationModel
                {
                    Op = "remove",
                    Path = $"members",
                    Value = JsonDocument.Parse(JsonSerializer.Serialize(existingMembers.Select(uid => new { value = uid }).ToArray())).RootElement
                }
            },
            Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
        };

        await sutProvider.Sut.PatchGroupAsync(organization, group.Id, scimPatchModel);

        await sutProvider.GetDependency<IGroupRepository>().Received(1).UpdateUsersAsync(group.Id, Arg.Is<IEnumerable<Guid>>(arg => arg.All(id => existingMembers.Contains(id))));
    }

    [Theory]
    [BitAutoData]
    public async Task PatchGroup_NoAction_Success(SutProvider<PatchGroupCommand> sutProvider, Organization organization, Group group)
    {
        group.OrganizationId = organization.Id;

        sutProvider.GetDependency<IGroupRepository>()
            .GetByIdAsync(group.Id)
            .Returns(group);

        var scimPatchModel = new Models.ScimPatchModel
        {
            Operations = new List<ScimPatchModel.OperationModel>(),
            Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
        };

        await sutProvider.Sut.PatchGroupAsync(organization, group.Id, scimPatchModel);

        await sutProvider.GetDependency<IGroupRepository>().DidNotReceiveWithAnyArgs().UpdateUsersAsync(default, default);
        await sutProvider.GetDependency<IGroupRepository>().DidNotReceiveWithAnyArgs().GetManyUserIdsByIdAsync(default);
        await sutProvider.GetDependency<IUpdateGroupCommand>().DidNotReceiveWithAnyArgs().UpdateGroupAsync(default, default);
        await sutProvider.GetDependency<IGroupRepository>().DidNotReceiveWithAnyArgs().DeleteUserAsync(default, default);
    }

    [Theory]
    [BitAutoData]
    public async Task PatchGroup_NotFound_Throws(SutProvider<PatchGroupCommand> sutProvider, Organization organization, Guid groupId)
    {
        var scimPatchModel = new Models.ScimPatchModel
        {
            Operations = new List<ScimPatchModel.OperationModel>(),
            Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
        };

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.PatchGroupAsync(organization, groupId, scimPatchModel));
    }

    [Theory]
    [BitAutoData]
    public async Task PatchGroup_MismatchingOrganizationId_Throws(SutProvider<PatchGroupCommand> sutProvider, Organization organization, Guid groupId)
    {
        var scimPatchModel = new Models.ScimPatchModel
        {
            Operations = new List<ScimPatchModel.OperationModel>(),
            Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
        };

        sutProvider.GetDependency<IGroupRepository>()
            .GetByIdAsync(groupId)
            .Returns(new Group
            {
                Id = groupId,
                OrganizationId = Guid.NewGuid()
            });

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.PatchGroupAsync(organization, groupId, scimPatchModel));
    }
}
