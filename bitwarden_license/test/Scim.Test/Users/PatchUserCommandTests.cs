using System.Text.Json;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.RestoreUser.v1;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.RevokeUser.v1;
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
public class PatchUserCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task PatchUser_RestorePath_Success(SutProvider<PatchUserCommand> sutProvider, OrganizationUser organizationUser)
    {
        organizationUser.Status = Core.Enums.OrganizationUserStatusType.Revoked;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUser.Id)
            .Returns(organizationUser);

        var scimPatchModel = new Models.ScimPatchModel
        {
            Operations = new List<ScimPatchModel.OperationModel>
            {
                new ScimPatchModel.OperationModel
                {
                    Op = "replace",
                    Path = "active",
                    Value = JsonDocument.Parse("true").RootElement
                }
            },
            Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
        };

        await sutProvider.Sut.PatchUserAsync(organizationUser.OrganizationId, organizationUser.Id, scimPatchModel);

        await sutProvider.GetDependency<IRestoreOrganizationUserCommand>().Received(1).RestoreUserAsync(organizationUser, EventSystemUser.SCIM);
    }

    [Theory]
    [BitAutoData]
    public async Task PatchUser_RestoreValue_Success(SutProvider<PatchUserCommand> sutProvider, OrganizationUser organizationUser)
    {
        organizationUser.Status = Core.Enums.OrganizationUserStatusType.Revoked;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUser.Id)
            .Returns(organizationUser);

        var scimPatchModel = new Models.ScimPatchModel
        {
            Operations = new List<ScimPatchModel.OperationModel>
            {
                new ScimPatchModel.OperationModel
                {
                    Op = "replace",
                    Value = JsonDocument.Parse("{\"active\":true}").RootElement
                }
            },
            Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
        };

        await sutProvider.Sut.PatchUserAsync(organizationUser.OrganizationId, organizationUser.Id, scimPatchModel);

        await sutProvider.GetDependency<IRestoreOrganizationUserCommand>().Received(1).RestoreUserAsync(organizationUser, EventSystemUser.SCIM);
    }

    [Theory]
    [BitAutoData]
    public async Task PatchUser_RevokePath_Success(SutProvider<PatchUserCommand> sutProvider, OrganizationUser organizationUser)
    {
        organizationUser.Status = Core.Enums.OrganizationUserStatusType.Confirmed;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUser.Id)
            .Returns(organizationUser);

        var scimPatchModel = new Models.ScimPatchModel
        {
            Operations = new List<ScimPatchModel.OperationModel>
            {
                new ScimPatchModel.OperationModel
                {
                    Op = "replace",
                    Path = "active",
                    Value = JsonDocument.Parse("false").RootElement
                }
            },
            Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
        };

        await sutProvider.Sut.PatchUserAsync(organizationUser.OrganizationId, organizationUser.Id, scimPatchModel);

        await sutProvider.GetDependency<IRevokeOrganizationUserCommand>().Received(1).RevokeUserAsync(organizationUser, EventSystemUser.SCIM);
    }

    [Theory]
    [BitAutoData]
    public async Task PatchUser_RevokeValue_Success(SutProvider<PatchUserCommand> sutProvider, OrganizationUser organizationUser)
    {
        organizationUser.Status = Core.Enums.OrganizationUserStatusType.Confirmed;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUser.Id)
            .Returns(organizationUser);

        var scimPatchModel = new Models.ScimPatchModel
        {
            Operations = new List<ScimPatchModel.OperationModel>
            {
                new ScimPatchModel.OperationModel
                {
                    Op = "replace",
                    Value = JsonDocument.Parse("{\"active\":false}").RootElement
                }
            },
            Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
        };

        await sutProvider.Sut.PatchUserAsync(organizationUser.OrganizationId, organizationUser.Id, scimPatchModel);

        await sutProvider.GetDependency<IRevokeOrganizationUserCommand>().Received(1).RevokeUserAsync(organizationUser, EventSystemUser.SCIM);
    }

    [Theory]
    [BitAutoData]
    public async Task PatchUser_NoAction_Success(SutProvider<PatchUserCommand> sutProvider, OrganizationUser organizationUser)
    {
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUser.Id)
            .Returns(organizationUser);

        var scimPatchModel = new Models.ScimPatchModel
        {
            Operations = new List<ScimPatchModel.OperationModel>(),
            Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
        };

        await sutProvider.Sut.PatchUserAsync(organizationUser.OrganizationId, organizationUser.Id, scimPatchModel);

        await sutProvider.GetDependency<IRestoreOrganizationUserCommand>().DidNotReceiveWithAnyArgs().RestoreUserAsync(default, EventSystemUser.SCIM);
        await sutProvider.GetDependency<IRevokeOrganizationUserCommand>().DidNotReceiveWithAnyArgs().RevokeUserAsync(default, EventSystemUser.SCIM);
    }

    [Theory]
    [BitAutoData]
    public async Task PatchUser_NotFound_Throws(SutProvider<PatchUserCommand> sutProvider, Guid organizationId, Guid organizationUserId)
    {
        var scimPatchModel = new Models.ScimPatchModel
        {
            Operations = new List<ScimPatchModel.OperationModel>(),
            Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
        };

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.PatchUserAsync(organizationId, organizationUserId, scimPatchModel));
    }

    [Theory]
    [BitAutoData]
    public async Task PatchUser_MismatchingOrganizationId_Throws(SutProvider<PatchUserCommand> sutProvider, Guid organizationId, Guid organizationUserId)
    {
        var scimPatchModel = new Models.ScimPatchModel
        {
            Operations = new List<ScimPatchModel.OperationModel>(),
            Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
        };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUserId)
            .Returns(new OrganizationUser
            {
                Id = organizationUserId,
                OrganizationId = Guid.NewGuid()
            });

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.PatchUserAsync(organizationId, organizationUserId, scimPatchModel));
    }

    [Theory]
    [BitAutoData]
    public async Task PatchUser_ExternalIdFromPath_Success(SutProvider<PatchUserCommand> sutProvider, OrganizationUser organizationUser)
    {
        var newExternalId = "new-external-id-123";
        organizationUser.ExternalId = "old-external-id";

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUser.Id)
            .Returns(organizationUser);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(organizationUser.OrganizationId)
            .Returns(new List<OrganizationUserUserDetails>());

        var scimPatchModel = new Models.ScimPatchModel
        {
            Operations = new List<ScimPatchModel.OperationModel>
            {
                new ScimPatchModel.OperationModel
                {
                    Op = "replace",
                    Path = "externalId",
                    Value = JsonDocument.Parse($"\"{newExternalId}\"").RootElement
                }
            },
            Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
        };

        await sutProvider.Sut.PatchUserAsync(organizationUser.OrganizationId, organizationUser.Id, scimPatchModel);

        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1).ReplaceAsync(
            Arg.Is<OrganizationUser>(ou => ou.ExternalId == newExternalId));
    }

    [Theory]
    [BitAutoData]
    public async Task PatchUser_ExternalIdFromValue_Success(SutProvider<PatchUserCommand> sutProvider, OrganizationUser organizationUser)
    {
        var newExternalId = "new-external-id-456";
        organizationUser.ExternalId = null;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUser.Id)
            .Returns(organizationUser);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(organizationUser.OrganizationId)
            .Returns(new List<OrganizationUserUserDetails>());

        var scimPatchModel = new Models.ScimPatchModel
        {
            Operations = new List<ScimPatchModel.OperationModel>
            {
                new ScimPatchModel.OperationModel
                {
                    Op = "replace",
                    Value = JsonDocument.Parse($"{{\"externalId\":\"{newExternalId}\"}}").RootElement
                }
            },
            Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
        };

        await sutProvider.Sut.PatchUserAsync(organizationUser.OrganizationId, organizationUser.Id, scimPatchModel);

        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1).ReplaceAsync(
            Arg.Is<OrganizationUser>(ou => ou.ExternalId == newExternalId));
    }

    [Theory]
    [BitAutoData]
    public async Task PatchUser_ExternalIdDuplicate_ThrowsConflict(SutProvider<PatchUserCommand> sutProvider, OrganizationUser organizationUser, OrganizationUserUserDetails existingUser)
    {
        var duplicateExternalId = "duplicate-id";
        organizationUser.ExternalId = "old-id";
        existingUser.ExternalId = duplicateExternalId;
        existingUser.Id = Guid.NewGuid(); // Different user

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUser.Id)
            .Returns(organizationUser);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(organizationUser.OrganizationId)
            .Returns(new List<OrganizationUserUserDetails> { existingUser });

        var scimPatchModel = new Models.ScimPatchModel
        {
            Operations = new List<ScimPatchModel.OperationModel>
            {
                new ScimPatchModel.OperationModel
                {
                    Op = "replace",
                    Path = "externalId",
                    Value = JsonDocument.Parse($"\"{duplicateExternalId}\"").RootElement
                }
            },
            Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
        };

        await Assert.ThrowsAsync<ConflictException>(async () =>
            await sutProvider.Sut.PatchUserAsync(organizationUser.OrganizationId, organizationUser.Id, scimPatchModel));
    }

    [Theory]
    [BitAutoData]
    public async Task PatchUser_ExternalIdTooLong_ThrowsBadRequest(SutProvider<PatchUserCommand> sutProvider, OrganizationUser organizationUser)
    {
        var tooLongExternalId = new string('a', 301); // Exceeds 300 character limit

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUser.Id)
            .Returns(organizationUser);

        var scimPatchModel = new Models.ScimPatchModel
        {
            Operations = new List<ScimPatchModel.OperationModel>
            {
                new ScimPatchModel.OperationModel
                {
                    Op = "replace",
                    Path = "externalId",
                    Value = JsonDocument.Parse($"\"{tooLongExternalId}\"").RootElement
                }
            },
            Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
        };

        await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sutProvider.Sut.PatchUserAsync(organizationUser.OrganizationId, organizationUser.Id, scimPatchModel));
    }

    [Theory]
    [BitAutoData]
    public async Task PatchUser_ExternalIdNull_Success(SutProvider<PatchUserCommand> sutProvider, OrganizationUser organizationUser)
    {
        organizationUser.ExternalId = "existing-id";

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUser.Id)
            .Returns(organizationUser);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(organizationUser.OrganizationId)
            .Returns(new List<OrganizationUserUserDetails>());

        var scimPatchModel = new Models.ScimPatchModel
        {
            Operations = new List<ScimPatchModel.OperationModel>
            {
                new ScimPatchModel.OperationModel
                {
                    Op = "replace",
                    Path = "externalId",
                    Value = JsonDocument.Parse("null").RootElement
                }
            },
            Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
        };

        await sutProvider.Sut.PatchUserAsync(organizationUser.OrganizationId, organizationUser.Id, scimPatchModel);

        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1).ReplaceAsync(
            Arg.Is<OrganizationUser>(ou => ou.ExternalId == null));
    }

    [Theory]
    [BitAutoData]
    public async Task PatchUser_UnsupportedOperation_LogsWarningAndSucceeds(SutProvider<PatchUserCommand> sutProvider, OrganizationUser organizationUser)
    {
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUser.Id)
            .Returns(organizationUser);

        var scimPatchModel = new Models.ScimPatchModel
        {
            Operations = new List<ScimPatchModel.OperationModel>
            {
                new ScimPatchModel.OperationModel
                {
                    Op = "add",
                    Path = "displayName",
                    Value = JsonDocument.Parse("\"John Doe\"").RootElement
                }
            },
            Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
        };

        // Should not throw - unsupported operations are logged as warnings but don't fail the request
        await sutProvider.Sut.PatchUserAsync(organizationUser.OrganizationId, organizationUser.Id, scimPatchModel);

        // Verify no restore or revoke operations were called
        await sutProvider.GetDependency<IRestoreOrganizationUserCommand>().DidNotReceiveWithAnyArgs().RestoreUserAsync(default, EventSystemUser.SCIM);
        await sutProvider.GetDependency<IRevokeOrganizationUserCommand>().DidNotReceiveWithAnyArgs().RevokeUserAsync(default, EventSystemUser.SCIM);
    }

    [Theory]
    [BitAutoData]
    public async Task PatchUser_ActiveAndExternalIdFromValue_Success(SutProvider<PatchUserCommand> sutProvider, OrganizationUser organizationUser)
    {
        var newExternalId = "combined-test-id";
        organizationUser.Status = OrganizationUserStatusType.Confirmed;
        organizationUser.ExternalId = "old-id";

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUser.Id)
            .Returns(organizationUser);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(organizationUser.OrganizationId)
            .Returns(new List<OrganizationUserUserDetails>());

        var scimPatchModel = new Models.ScimPatchModel
        {
            Operations = new List<ScimPatchModel.OperationModel>
            {
                new ScimPatchModel.OperationModel
                {
                    Op = "replace",
                    Value = JsonDocument.Parse($"{{\"active\":false,\"externalId\":\"{newExternalId}\"}}").RootElement
                }
            },
            Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
        };

        await sutProvider.Sut.PatchUserAsync(organizationUser.OrganizationId, organizationUser.Id, scimPatchModel);

        // Verify both operations were processed
        await sutProvider.GetDependency<IRevokeOrganizationUserCommand>().Received(1).RevokeUserAsync(organizationUser, EventSystemUser.SCIM);
        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1).ReplaceAsync(
            Arg.Is<OrganizationUser>(ou => ou.ExternalId == newExternalId));
    }

    [Theory]
    [BitAutoData]
    public async Task PatchUser_RestoreAndExternalIdFromValue_DoesNotRevertRestore(SutProvider<PatchUserCommand> sutProvider, OrganizationUser organizationUser)
    {
        var newExternalId = "combined-restore-id";
        organizationUser.Status = OrganizationUserStatusType.Revoked;
        organizationUser.ExternalId = "old-id";

        // Simulate the re-fetch after restore returning a user with a non-revoked status
        var restoredOrgUser = new OrganizationUser
        {
            Id = organizationUser.Id,
            OrganizationId = organizationUser.OrganizationId,
            Status = OrganizationUserStatusType.Confirmed,
            ExternalId = organizationUser.ExternalId,
        };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUser.Id)
            .Returns(organizationUser, restoredOrgUser);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(organizationUser.OrganizationId)
            .Returns(new List<OrganizationUserUserDetails>());

        var scimPatchModel = new Models.ScimPatchModel
        {
            Operations = new List<ScimPatchModel.OperationModel>
            {
                new ScimPatchModel.OperationModel
                {
                    Op = "replace",
                    Value = JsonDocument.Parse($"{{\"active\":true,\"externalId\":\"{newExternalId}\"}}").RootElement
                }
            },
            Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
        };

        await sutProvider.Sut.PatchUserAsync(organizationUser.OrganizationId, organizationUser.Id, scimPatchModel);

        await sutProvider.GetDependency<IRestoreOrganizationUserCommand>().Received(1).RestoreUserAsync(organizationUser, EventSystemUser.SCIM);
        // ReplaceAsync must use the re-fetched (restored) user, not the stale revoked state
        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1).ReplaceAsync(
            Arg.Is<OrganizationUser>(ou => ou.ExternalId == newExternalId && ou.Status != OrganizationUserStatusType.Revoked));
    }
}
