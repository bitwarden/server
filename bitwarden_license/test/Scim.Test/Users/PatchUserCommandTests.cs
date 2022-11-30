using System.Text.Json;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
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

        await sutProvider.GetDependency<IOrganizationService>().Received(1).RestoreUserAsync(organizationUser, EventSystemUser.SCIM, Arg.Any<IUserService>());
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

        await sutProvider.GetDependency<IOrganizationService>().Received(1).RestoreUserAsync(organizationUser, EventSystemUser.SCIM, Arg.Any<IUserService>());
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

        await sutProvider.GetDependency<IOrganizationService>().Received(1).RevokeUserAsync(organizationUser, EventSystemUser.SCIM);
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

        await sutProvider.GetDependency<IOrganizationService>().Received(1).RevokeUserAsync(organizationUser, EventSystemUser.SCIM);
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

        await sutProvider.GetDependency<IOrganizationService>().Received(0).RestoreUserAsync(organizationUser, EventSystemUser.SCIM, Arg.Any<IUserService>());
        await sutProvider.GetDependency<IOrganizationService>().Received(0).RevokeUserAsync(organizationUser, EventSystemUser.SCIM);
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
}
