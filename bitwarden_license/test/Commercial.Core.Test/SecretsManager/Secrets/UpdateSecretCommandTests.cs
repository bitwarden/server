using Bit.Commercial.Core.SecretsManager.Commands.Secrets;
using Bit.Commercial.Core.Test.SecretsManager.Enums;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Test.SecretsManager.AutoFixture.ProjectsFixture;
using Bit.Core.Test.SecretsManager.AutoFixture.SecretsFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using NSubstitute;
using Xunit;

namespace Bit.Commercial.Core.Test.SecretsManager.Secrets;

[SutProviderCustomize]
[SecretCustomize]
[ProjectCustomize]
public class UpdateSecretCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task UpdateAsync_SecretDoesNotExist_ThrowsNotFound(Secret data, SutProvider<UpdateSecretCommand> sutProvider)
    {
        var exception = await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.UpdateAsync(data, default));

        await sutProvider.GetDependency<ISecretRepository>().DidNotReceiveWithAnyArgs().UpdateAsync(default);
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsAdmin)]
    [BitAutoData(PermissionType.RunAsUserWithPermission)]
    public async Task UpdateAsync_Success(PermissionType permissionType, Secret data, SutProvider<UpdateSecretCommand> sutProvider, Guid userId, Project mockProject)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(data.OrganizationId).Returns(true);
        data.Projects = new List<Project>() { mockProject };

        if (permissionType == PermissionType.RunAsAdmin)
        {
            sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(data.OrganizationId).Returns(true);
        }
        else
        {
            sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(data.OrganizationId).Returns(false);
            sutProvider.GetDependency<IProjectRepository>().UserHasWriteAccessToProject((Guid)(data.Projects?.First().Id), userId).Returns(true);
        }

        sutProvider.GetDependency<ISecretRepository>().GetByIdAsync(data.Id).Returns(data);
        await sutProvider.Sut.UpdateAsync(data, userId);

        await sutProvider.GetDependency<ISecretRepository>().Received(1)
            .UpdateAsync(data);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateAsync_DoesNotModifyOrganizationId(Secret existingSecret, SutProvider<UpdateSecretCommand> sutProvider, Guid userId)
    {
        var updatedOrgId = Guid.NewGuid();
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(existingSecret.OrganizationId).Returns(true);
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(existingSecret.OrganizationId).Returns(true);
        sutProvider.GetDependency<ISecretRepository>().GetByIdAsync(existingSecret.Id).Returns(existingSecret);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(updatedOrgId).Returns(true);

        var secretUpdate = new Secret()
        {
            OrganizationId = updatedOrgId,
            Id = existingSecret.Id,
            Key = existingSecret.Key,
        };

        var result = await sutProvider.Sut.UpdateAsync(secretUpdate, userId);

        Assert.Equal(existingSecret.OrganizationId, result.OrganizationId);
        Assert.NotEqual(existingSecret.OrganizationId, updatedOrgId);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateAsync_DoesNotModifyCreationDate(Secret existingSecret, SutProvider<UpdateSecretCommand> sutProvider, Guid userId)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(existingSecret.OrganizationId).Returns(true);
        sutProvider.GetDependency<ISecretRepository>().GetByIdAsync(existingSecret.Id).Returns(existingSecret);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(existingSecret.OrganizationId).Returns(true);

        var updatedCreationDate = DateTime.UtcNow;
        var secretUpdate = new Secret()
        {
            CreationDate = updatedCreationDate,
            Id = existingSecret.Id,
            Key = existingSecret.Key,
            OrganizationId = existingSecret.OrganizationId
        };

        var result = await sutProvider.Sut.UpdateAsync(secretUpdate, userId);

        Assert.Equal(existingSecret.CreationDate, result.CreationDate);
        Assert.NotEqual(existingSecret.CreationDate, updatedCreationDate);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateAsync_DoesNotModifyDeletionDate(Secret existingSecret, SutProvider<UpdateSecretCommand> sutProvider, Guid userId)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(existingSecret.OrganizationId).Returns(true);
        sutProvider.GetDependency<ISecretRepository>().GetByIdAsync(existingSecret.Id).Returns(existingSecret);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(existingSecret.OrganizationId).Returns(true);

        var updatedDeletionDate = DateTime.UtcNow;
        var secretUpdate = new Secret()
        {
            DeletedDate = updatedDeletionDate,
            Id = existingSecret.Id,
            Key = existingSecret.Key,
            OrganizationId = existingSecret.OrganizationId
        };

        var result = await sutProvider.Sut.UpdateAsync(secretUpdate, userId);

        Assert.Equal(existingSecret.DeletedDate, result.DeletedDate);
        Assert.NotEqual(existingSecret.DeletedDate, updatedDeletionDate);
    }


    [Theory]
    [BitAutoData]
    public async Task UpdateAsync_RevisionDateIsUpdatedToUtcNow(Secret existingSecret, SutProvider<UpdateSecretCommand> sutProvider, Guid userId)
    {
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(existingSecret.OrganizationId).Returns(true);
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(existingSecret.OrganizationId).Returns(true);
        sutProvider.GetDependency<ISecretRepository>().GetByIdAsync(existingSecret.Id).Returns(existingSecret);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(existingSecret.OrganizationId).Returns(true);

        var updatedRevisionDate = DateTime.UtcNow.AddDays(10);
        var secretUpdate = new Secret()
        {
            RevisionDate = updatedRevisionDate,
            Id = existingSecret.Id,
            Key = existingSecret.Key,
            OrganizationId = existingSecret.OrganizationId
        };

        var result = await sutProvider.Sut.UpdateAsync(secretUpdate, userId);

        Assert.NotEqual(secretUpdate.RevisionDate, result.RevisionDate);
        AssertHelper.AssertRecent(result.RevisionDate);
    }
}

