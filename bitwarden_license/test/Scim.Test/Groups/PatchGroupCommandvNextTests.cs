﻿using System.Text.Json;
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
    public async Task PatchGroup_ReplaceListMembers_Success(SutProvider<PatchGroupCommandvNext> sutProvider, Organization organization, Group group, IEnumerable<Guid> userIds)
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

        await sutProvider.GetDependency<IGroupRepository>().Received(1).UpdateUsersAsync(group.Id, Arg.Is<IEnumerable<Guid>>(arg => arg.All(id => userIds.Contains(id))));
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

        await sutProvider.Sut.PatchGroupAsync(group, scimPatchModel);

        await sutProvider.GetDependency<IGroupRepository>().Received(1).AddGroupUsersByIdAsync(group.Id, Arg.Is<IEnumerable<Guid>>(arg => arg.All(id => existingMembers.Append(userId).Contains(id))));
    }

    [Theory]
    [BitAutoData]
    public async Task PatchGroup_AddListMembers_Success(SutProvider<PatchGroupCommandvNext> sutProvider, Organization organization, Group group, ICollection<Guid> existingMembers, ICollection<Guid> userIds)
    {
        group.OrganizationId = organization.Id;

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

        await sutProvider.Sut.PatchGroupAsync(group, scimPatchModel);

        await sutProvider.GetDependency<IGroupRepository>().Received(1).AddGroupUsersByIdAsync(group.Id, Arg.Is<IEnumerable<Guid>>(arg => arg.All(id => existingMembers.Concat(userIds).Contains(id))));
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
    public async Task PatchGroup_RemoveListMembers_Success(SutProvider<PatchGroupCommandvNext> sutProvider, Organization organization, Group group, ICollection<Guid> existingMembers)
    {
        group.OrganizationId = organization.Id;

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

        await sutProvider.Sut.PatchGroupAsync(group, scimPatchModel);

        await sutProvider.GetDependency<IGroupRepository>().Received(1).UpdateUsersAsync(group.Id, Arg.Is<IEnumerable<Guid>>(arg => arg.All(id => existingMembers.Contains(id))));
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
