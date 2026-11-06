using System.Text.Json;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.RestoreUser.v1;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.RevokeUser.v1;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Scim.Models;
using Bit.Scim.Users;
using Bit.Scim.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Scim.Test.Users;

[SutProviderCustomize]
public class PatchUserCommandGroupTests
{
    [Theory]
    [BitAutoData]
    public async Task PatchUser_AddGroupOperation_Success(
        SutProvider<PatchUserCommand> sutProvider, 
        OrganizationUser organizationUser,
        Guid groupId)
    {
        // Arrange
        organizationUser.Status = OrganizationUserStatusType.Confirmed;
        
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUser.Id)
            .Returns(organizationUser);

        sutProvider.GetDependency<IGroupRepository>()
            .GetByIdAsync(groupId)
            .Returns(new Group { Id = groupId, OrganizationId = organizationUser.OrganizationId });

        var scimPatchModel = new ScimPatchModel
        {
            Operations = new List<ScimPatchModel.OperationModel>
            {
                new ScimPatchModel.OperationModel
                {
                    Op = "add",
                    Path = "groups",
                    Value = JsonDocument.Parse($"{{\"value\":\"{groupId}\"}}").RootElement
                }
            },
            Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
        };

        // Act
        await sutProvider.Sut.PatchUserAsync(organizationUser.OrganizationId, organizationUser.Id, scimPatchModel);

        // Assert
        await sutProvider.GetDependency<IGroupRepository>().Received(1)
            .AddGroupUsersByIdAsync(groupId, Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(organizationUser.Id)), Arg.Any<DateTime>());
    }

    [Theory]
    [BitAutoData]
    public async Task PatchUser_RemoveGroupOperation_Success(
        SutProvider<PatchUserCommand> sutProvider, 
        OrganizationUser organizationUser,
        Guid groupId)
    {
        // Arrange
        organizationUser.Status = OrganizationUserStatusType.Confirmed;
        
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUser.Id)
            .Returns(organizationUser);

        sutProvider.GetDependency<IGroupRepository>()
            .GetByIdAsync(groupId)
            .Returns(new Group { Id = groupId, OrganizationId = organizationUser.OrganizationId });

        var scimPatchModel = new ScimPatchModel
        {
            Operations = new List<ScimPatchModel.OperationModel>
            {
                new ScimPatchModel.OperationModel
                {
                    Op = "remove",
                    Path = "groups",
                    Value = JsonDocument.Parse($"{{\"value\":\"{groupId}\"}}").RootElement
                }
            },
            Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
        };

        // Act
        await sutProvider.Sut.PatchUserAsync(organizationUser.OrganizationId, organizationUser.Id, scimPatchModel);

        // Assert
        await sutProvider.GetDependency<IGroupRepository>().Received(1)
            .DeleteUserAsync(groupId, organizationUser.Id, Arg.Any<DateTime>());
    }

    [Theory]
    [BitAutoData]
    public async Task PatchUser_ReplaceGroupsOperation_Success(
        SutProvider<PatchUserCommand> sutProvider, 
        OrganizationUser organizationUser,
        Guid groupId1,
        Guid groupId2)
    {
        // Arrange
        organizationUser.Status = OrganizationUserStatusType.Confirmed;
        
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUser.Id)
            .Returns(organizationUser);

        sutProvider.GetDependency<IGroupRepository>()
            .GetByIdAsync(groupId1)
            .Returns(new Group { Id = groupId1, OrganizationId = organizationUser.OrganizationId });

        sutProvider.GetDependency<IGroupRepository>()
            .GetByIdAsync(groupId2)
            .Returns(new Group { Id = groupId2, OrganizationId = organizationUser.OrganizationId });

        var scimPatchModel = new ScimPatchModel
        {
            Operations = new List<ScimPatchModel.OperationModel>
            {
                new ScimPatchModel.OperationModel
                {
                    Op = "replace",
                    Path = "groups",
                    Value = JsonDocument.Parse($"[{{\"value\":\"{groupId1}\"}},{{\"value\":\"{groupId2}\"}}]").RootElement
                }
            },
            Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
        };

        // Act
        await sutProvider.Sut.PatchUserAsync(organizationUser.OrganizationId, organizationUser.Id, scimPatchModel);

        // Assert
        await sutProvider.GetDependency<IGroupRepository>().Received(1)
            .UpdateUsersAsync(groupId1, Arg.Any<IEnumerable<Guid>>(), Arg.Any<DateTime>());
        await sutProvider.GetDependency<IGroupRepository>().Received(1)
            .UpdateUsersAsync(groupId2, Arg.Any<IEnumerable<Guid>>(), Arg.Any<DateTime>());
    }

    [Theory]
    [BitAutoData]
    public async Task PatchUser_AddGroupInvalidGroupId_ThrowsNotFound(
        SutProvider<PatchUserCommand> sutProvider, 
        OrganizationUser organizationUser,
        Guid groupId)
    {
        // Arrange
        organizationUser.Status = OrganizationUserStatusType.Confirmed;
        
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUser.Id)
            .Returns(organizationUser);

        sutProvider.GetDependency<IGroupRepository>()
            .GetByIdAsync(groupId)
            .Returns((Group)null);

        var scimPatchModel = new ScimPatchModel
        {
            Operations = new List<ScimPatchModel.OperationModel>
            {
                new ScimPatchModel.OperationModel
                {
                    Op = "add",
                    Path = "groups",
                    Value = JsonDocument.Parse($"{{\"value\":\"{groupId}\"}}").RootElement
                }
            },
            Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
        };

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.PatchUserAsync(organizationUser.OrganizationId, organizationUser.Id, scimPatchModel));
    }

    [Theory]
    [BitAutoData]
    public async Task PatchUser_AddGroupCrossOrganization_ThrowsBadRequest(
        SutProvider<PatchUserCommand> sutProvider, 
        OrganizationUser organizationUser,
        Guid groupId,
        Guid differentOrgId)
    {
        // Arrange
        organizationUser.Status = OrganizationUserStatusType.Confirmed;
        
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUser.Id)
            .Returns(organizationUser);

        sutProvider.GetDependency<IGroupRepository>()
            .GetByIdAsync(groupId)
            .Returns(new Group { Id = groupId, OrganizationId = differentOrgId });

        var scimPatchModel = new ScimPatchModel
        {
            Operations = new List<ScimPatchModel.OperationModel>
            {
                new ScimPatchModel.OperationModel
                {
                    Op = "add",
                    Path = "groups",
                    Value = JsonDocument.Parse($"{{\"value\":\"{groupId}\"}}").RootElement
                }
            },
            Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
        };

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.PatchUserAsync(organizationUser.OrganizationId, organizationUser.Id, scimPatchModel));
    }

    [Theory]
    [BitAutoData]
    public async Task PatchUser_RemoveGroupInvalidGroupId_ThrowsNotFound(
        SutProvider<PatchUserCommand> sutProvider, 
        OrganizationUser organizationUser,
        Guid groupId)
    {
        // Arrange
        organizationUser.Status = OrganizationUserStatusType.Confirmed;
        
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUser.Id)
            .Returns(organizationUser);

        sutProvider.GetDependency<IGroupRepository>()
            .GetByIdAsync(groupId)
            .Returns((Group)null);

        var scimPatchModel = new ScimPatchModel
        {
            Operations = new List<ScimPatchModel.OperationModel>
            {
                new ScimPatchModel.OperationModel
                {
                    Op = "remove",
                    Path = "groups",
                    Value = JsonDocument.Parse($"{{\"value\":\"{groupId}\"}}").RootElement
                }
            },
            Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
        };

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.PatchUserAsync(organizationUser.OrganizationId, organizationUser.Id, scimPatchModel));
    }
}
