using System.Text.Json;
using AutoFixture;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.Groups.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Scim.Groups;
using Bit.Scim.Models;
using Bit.Scim.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Scim.Test.Groups;

[SutProviderCustomize]
public class PatchGroupCommandTests
{
    private static readonly DateTime _expectedRevisionDate = DateTime.UtcNow.AddYears(1);

    [Theory]
    [BitAutoData]
    public async Task PatchGroup_ReplaceListMembers_Success(
        Organization organization, Group group, IEnumerable<Guid> userIds)
    {
        var sutProvider = SetupSutProvider();
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
                arg.ToHashSet().SetEquals(userIds)),
            Arg.Is<DateTime>(d => d == _expectedRevisionDate));
    }

    [Theory]
    [BitAutoData]
    public async Task PatchGroup_ReplaceDisplayNameFromPath_Success(
        SutProvider<PatchGroupCommand> sutProvider, Organization organization, Group group, string displayName)
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
    public async Task PatchGroup_ReplaceDisplayNameFromPath_MissingOrganization_Throws(
        SutProvider<PatchGroupCommand> sutProvider, Organization organization, Group group, string displayName)
    {
        group.OrganizationId = organization.Id;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns((Organization)null);

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

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.PatchGroupAsync(group, scimPatchModel));
    }

    [Theory]
    [BitAutoData]
    public async Task PatchGroup_ReplaceDisplayNameFromValueObject_Success(SutProvider<PatchGroupCommand> sutProvider, Organization organization, Group group, string displayName)
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
    public async Task PatchGroup_ReplaceDisplayNameFromValueObject_MissingOrganization_Throws(
        SutProvider<PatchGroupCommand> sutProvider, Organization organization, Group group, string displayName)
    {
        group.OrganizationId = organization.Id;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns((Organization)null);

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

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.PatchGroupAsync(group, scimPatchModel));
    }

    [Theory]
    [BitAutoData]
    public async Task PatchGroup_AddSingleMember_Success(Organization organization, Group group, ICollection<Guid> existingMembers, Guid userId)
    {
        var sutProvider = SetupSutProvider();
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
            Arg.Is<IEnumerable<Guid>>(arg => arg.Single() == userId),
            Arg.Is<DateTime>(d => d == _expectedRevisionDate));
    }

    [Theory]
    [BitAutoData]
    public async Task PatchGroup_AddSingleMember_ReturnsEarlyIfAlreadyInGroup(
        SutProvider<PatchGroupCommand> sutProvider,
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
            .AddGroupUsersByIdAsync(default, default, default);
    }

    [Theory]
    [BitAutoData]
    public async Task PatchGroup_AddListMembers_Success(Organization organization, Group group, ICollection<Guid> existingMembers, ICollection<Guid> userIds)
    {
        var sutProvider = SetupSutProvider();
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
                arg.ToHashSet().SetEquals(userIds)),
            Arg.Is<DateTime>(d => d == _expectedRevisionDate));
    }

    [Theory]
    [BitAutoData]
    public async Task PatchGroup_AddListMembers_IgnoresDuplicatesInRequest(
        Organization organization, Group group,
        ICollection<Guid> existingMembers)
    {
        var sutProvider = SetupSutProvider();

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
                arg.ToHashSet().SetEquals(userIds)),
            Arg.Is<DateTime>(d => d == _expectedRevisionDate));
    }

    [Theory]
    [BitAutoData]
    public async Task PatchGroup_AddListMembers_SuccessIfOnlySomeUsersAreInGroup(
        Organization organization, Group group,
        ICollection<Guid> existingMembers,
        ICollection<Guid> userIds)
    {
        var sutProvider = SetupSutProvider();

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
                    arg.ToHashSet().SetEquals(userIds)),
                Arg.Is<DateTime>(d => d == _expectedRevisionDate));
    }

    [Theory]
    [BitAutoData]
    public async Task PatchGroup_RemoveSingleMember_Success(SutProvider<PatchGroupCommand> sutProvider, Organization organization, Group group, Guid userId)
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
    public async Task PatchGroup_RemoveListMembers_Success(
        Organization organization, Group group, ICollection<Guid> existingMembers)
    {
        var sutProvider = SetupSutProvider();
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
                    arg.ToHashSet().SetEquals(expectedRemainingUsers)),
                Arg.Is<DateTime>(d => d == _expectedRevisionDate));
    }

    [Theory]
    [BitAutoData]
    public async Task PatchGroup_ReplaceExternalIdFromPath_Success(
        Organization organization, Group group, string newExternalId)
    {
        // Arrange
        var sutProvider = SetupSutProvider();
        group.OrganizationId = organization.Id;

        sutProvider.GetDependency<IGroupRepository>()
            .GetManyByOrganizationIdAsync(organization.Id)
            .Returns(new List<Group>());

        var scimPatchModel = new ScimPatchModel
        {
            Operations = new List<ScimPatchModel.OperationModel>
            {
                new()
                {
                    Op = "replace",
                    Path = "externalId",
                    Value = JsonDocument.Parse($"\"{newExternalId}\"").RootElement
                }
            },
            Schemas = new List<string> { ScimConstants.Scim2SchemaGroup }
        };

        // Act
        await sutProvider.Sut.PatchGroupAsync(group, scimPatchModel);

        // Assert
        Assert.Equal(newExternalId, group.ExternalId);
        Assert.Equal(_expectedRevisionDate, group.RevisionDate);
        await sutProvider.GetDependency<IGroupRepository>().Received(1).ReplaceAsync(group);
    }

    [Theory]
    [BitAutoData]
    public async Task PatchGroup_AddExternalIdFromPath_Success(
        Organization organization, Group group, string newExternalId)
    {
        // Arrange
        var sutProvider = SetupSutProvider();
        group.OrganizationId = organization.Id;
        group.ExternalId = null;

        sutProvider.GetDependency<IGroupRepository>()
            .GetManyByOrganizationIdAsync(organization.Id)
            .Returns(new List<Group>());

        var scimPatchModel = new ScimPatchModel
        {
            Operations = new List<ScimPatchModel.OperationModel>
            {
                new()
                {
                    Op = "add",
                    Path = "externalId",
                    Value = JsonDocument.Parse($"\"{newExternalId}\"").RootElement
                }
            },
            Schemas = new List<string> { ScimConstants.Scim2SchemaGroup }
        };

        // Act
        await sutProvider.Sut.PatchGroupAsync(group, scimPatchModel);

        // Assert
        Assert.Equal(newExternalId, group.ExternalId);
        Assert.Equal(_expectedRevisionDate, group.RevisionDate);
        await sutProvider.GetDependency<IGroupRepository>().Received(1).ReplaceAsync(group);
    }

    [Theory]
    [BitAutoData]
    public async Task PatchGroup_ReplaceExternalIdFromValueObject_Success(
        Organization organization, Group group, string newExternalId)
    {
        // Arrange
        var sutProvider = SetupSutProvider();
        group.OrganizationId = organization.Id;

        sutProvider.GetDependency<IGroupRepository>()
            .GetManyByOrganizationIdAsync(organization.Id)
            .Returns(new List<Group>());

        var scimPatchModel = new ScimPatchModel
        {
            Operations = new List<ScimPatchModel.OperationModel>
            {
                new()
                {
                    Op = "replace",
                    Value = JsonDocument.Parse($"{{\"externalId\":\"{newExternalId}\"}}").RootElement
                }
            },
            Schemas = new List<string> { ScimConstants.Scim2SchemaGroup }
        };

        // Act
        await sutProvider.Sut.PatchGroupAsync(group, scimPatchModel);

        // Assert
        Assert.Equal(newExternalId, group.ExternalId);
        await sutProvider.GetDependency<IGroupRepository>().Received(1).ReplaceAsync(group);
    }

    [Theory]
    [BitAutoData]
    public async Task PatchGroup_AddExternalIdFromValueObject_Success(
        Organization organization, Group group, string newExternalId)
    {
        // Arrange
        var sutProvider = SetupSutProvider();
        group.OrganizationId = organization.Id;
        group.ExternalId = null;

        sutProvider.GetDependency<IGroupRepository>()
            .GetManyByOrganizationIdAsync(organization.Id)
            .Returns(new List<Group>());

        var scimPatchModel = new ScimPatchModel
        {
            Operations = new List<ScimPatchModel.OperationModel>
            {
                new()
                {
                    Op = "add",
                    Value = JsonDocument.Parse($"{{\"externalId\":\"{newExternalId}\"}}").RootElement
                }
            },
            Schemas = new List<string> { ScimConstants.Scim2SchemaGroup }
        };

        // Act
        await sutProvider.Sut.PatchGroupAsync(group, scimPatchModel);

        // Assert
        Assert.Equal(newExternalId, group.ExternalId);
        await sutProvider.GetDependency<IGroupRepository>().Received(1).ReplaceAsync(group);
    }

    [Theory]
    [BitAutoData]
    public async Task PatchGroup_AddExternalIdFromPath_DuplicateExternalId_Throws(
        Organization organization, Group group, Group otherGroup, string newExternalId)
    {
        // Arrange
        var sutProvider = SetupSutProvider();
        group.OrganizationId = organization.Id;
        otherGroup.OrganizationId = organization.Id;
        otherGroup.ExternalId = newExternalId;

        sutProvider.GetDependency<IGroupRepository>()
            .GetManyByOrganizationIdAsync(organization.Id)
            .Returns(new List<Group> { otherGroup });

        var scimPatchModel = new ScimPatchModel
        {
            Operations = new List<ScimPatchModel.OperationModel>
            {
                new()
                {
                    Op = "add",
                    Path = "externalId",
                    Value = JsonDocument.Parse($"\"{newExternalId}\"").RootElement
                }
            },
            Schemas = new List<string> { ScimConstants.Scim2SchemaGroup }
        };

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(() => sutProvider.Sut.PatchGroupAsync(group, scimPatchModel));
        await sutProvider.GetDependency<IGroupRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default);
    }

    [Theory]
    [BitAutoData]
    public async Task PatchGroup_AddExternalIdFromPath_TooLong_Throws(
        Organization organization, Group group)
    {
        // Arrange
        var sutProvider = SetupSutProvider();
        group.OrganizationId = organization.Id;
        var tooLongExternalId = new string('a', 301);

        var scimPatchModel = new ScimPatchModel
        {
            Operations = new List<ScimPatchModel.OperationModel>
            {
                new()
                {
                    Op = "add",
                    Path = "externalId",
                    Value = JsonDocument.Parse($"\"{tooLongExternalId}\"").RootElement
                }
            },
            Schemas = new List<string> { ScimConstants.Scim2SchemaGroup }
        };

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.PatchGroupAsync(group, scimPatchModel));
        await sutProvider.GetDependency<IGroupRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default);
    }

    [Theory]
    [BitAutoData]
    public async Task PatchGroup_AddExternalIdFromPath_SameGroupExistingExternalId_DoesNotConflict(
        Organization organization, Group group, string newExternalId)
    {
        // Arrange
        var sutProvider = SetupSutProvider();
        group.OrganizationId = organization.Id;
        group.ExternalId = newExternalId;

        sutProvider.GetDependency<IGroupRepository>()
            .GetManyByOrganizationIdAsync(organization.Id)
            .Returns(new List<Group> { group });

        var scimPatchModel = new ScimPatchModel
        {
            Operations = new List<ScimPatchModel.OperationModel>
            {
                new()
                {
                    Op = "add",
                    Path = "externalId",
                    Value = JsonDocument.Parse($"\"{newExternalId}\"").RootElement
                }
            },
            Schemas = new List<string> { ScimConstants.Scim2SchemaGroup }
        };

        // Act
        await sutProvider.Sut.PatchGroupAsync(group, scimPatchModel);

        // Assert
        Assert.Equal(newExternalId, group.ExternalId);
        await sutProvider.GetDependency<IGroupRepository>().Received(1).ReplaceAsync(group);
    }

    [Theory]
    [BitAutoData("")]
    [BitAutoData("   ")]
    public async Task PatchGroup_ReplaceExternalIdFromPath_EmptyOrWhitespace_StoresNull(
        string externalId,
        Organization organization,
        Group group)
    {
        // Arrange
        var sutProvider = SetupSutProvider();
        group.OrganizationId = organization.Id;
        group.ExternalId = "original";

        var scimPatchModel = new ScimPatchModel
        {
            Operations = new List<ScimPatchModel.OperationModel>
            {
                new()
                {
                    Op = "replace",
                    Path = "externalId",
                    Value = JsonDocument.Parse($"\"{externalId}\"").RootElement
                }
            },
            Schemas = new List<string> { ScimConstants.Scim2SchemaGroup }
        };

        // Act
        await sutProvider.Sut.PatchGroupAsync(group, scimPatchModel);

        // Assert
        await sutProvider.GetDependency<IGroupRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetManyByOrganizationIdAsync(default);
        await sutProvider.GetDependency<IGroupRepository>().Received(1).ReplaceAsync(group);
        Assert.Null(group.ExternalId);
    }

    [Theory]
    [BitAutoData]
    public async Task PatchGroup_InvalidOperation_Success(SutProvider<PatchGroupCommand> sutProvider, Organization organization, Group group)
    {
        group.OrganizationId = organization.Id;

        var scimPatchModel = new Models.ScimPatchModel
        {
            Operations = [new ScimPatchModel.OperationModel { Op = "invalid operation" }],
            Schemas = [ScimConstants.Scim2SchemaUser]
        };

        await sutProvider.Sut.PatchGroupAsync(group, scimPatchModel);

        // Assert: no operation performed
        await sutProvider.GetDependency<IGroupRepository>().DidNotReceiveWithAnyArgs().UpdateUsersAsync(default, default, default);
        await sutProvider.GetDependency<IGroupRepository>().DidNotReceiveWithAnyArgs().GetManyUserIdsByIdAsync(default);
        await sutProvider.GetDependency<IUpdateGroupCommand>().DidNotReceiveWithAnyArgs().UpdateGroupAsync(default, default);
        await sutProvider.GetDependency<IGroupService>().DidNotReceiveWithAnyArgs().DeleteUserAsync(default, default);

        // Assert: logging
        sutProvider.GetDependency<ILogger<PatchGroupCommand>>().ReceivedWithAnyArgs().LogWarning("");
    }

    [Theory]
    [BitAutoData]
    public async Task PatchGroup_NoOperation_Success(
        SutProvider<PatchGroupCommand> sutProvider, Organization organization, Group group)
    {
        group.OrganizationId = organization.Id;

        var scimPatchModel = new Models.ScimPatchModel
        {
            Operations = new List<ScimPatchModel.OperationModel>(),
            Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
        };

        await sutProvider.Sut.PatchGroupAsync(group, scimPatchModel);

        await sutProvider.GetDependency<IGroupRepository>().DidNotReceiveWithAnyArgs().UpdateUsersAsync(default, default, default);
        await sutProvider.GetDependency<IGroupRepository>().DidNotReceiveWithAnyArgs().GetManyUserIdsByIdAsync(default);
        await sutProvider.GetDependency<IUpdateGroupCommand>().DidNotReceiveWithAnyArgs().UpdateGroupAsync(default, default);
        await sutProvider.GetDependency<IGroupService>().DidNotReceiveWithAnyArgs().DeleteUserAsync(default, default);
    }

    private static SutProvider<PatchGroupCommand> SetupSutProvider()
    {
        var sutProvider = new SutProvider<PatchGroupCommand>()
            .WithFakeTimeProvider()
            .Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_expectedRevisionDate);
        return sutProvider;
    }
}
