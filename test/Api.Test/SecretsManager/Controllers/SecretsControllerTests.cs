using Bit.Api.SecretsManager.Controllers;
using Bit.Api.SecretsManager.Models.Request;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Commands.Secrets.Interfaces;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.SecretsManager.AutoFixture.SecretsFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using NSubstitute;
using Xunit;
using static Bit.Api.Test.SecretsManager.Controllers.AccessPoliciesControllerTests;

namespace Bit.Api.Test.SecretsManager.Controllers;

[ControllerCustomize(typeof(SecretsController))]
[SutProviderCustomize]
[JsonDocumentCustomize]
[SecretCustomize]
public class SecretsControllerTests
{
    [Theory]
    [BitAutoData]
    public async void GetSecretsByOrganization_ReturnsEmptyList(SutProvider<SecretsController> sutProvider, Guid id, Guid organizationId, Guid userId, AccessClientType accessType)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(id).Returns(true);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(organizationId).Returns(true);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);

        var result = await sutProvider.Sut.ListByOrganizationAsync(id);

        await sutProvider.GetDependency<ISecretRepository>().Received(1)
                     .GetManyByOrganizationIdAsync(Arg.Is(AssertHelper.AssertPropertyEqual(id)), userId, accessType);

        Assert.Empty(result.Secrets);
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsAdmin)]
    [BitAutoData(PermissionType.RunAsUserWithPermission)]
    public async void GetSecretsByOrganization_Success(PermissionType permissionType, SutProvider<SecretsController> sutProvider, Core.SecretsManager.Entities.Secret resultSecret, Guid organizationId, Guid userId, Core.SecretsManager.Entities.Project mockProject, AccessClientType accessType)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(default).ReturnsForAnyArgs(true);
        sutProvider.GetDependency<ISecretRepository>().GetManyByOrganizationIdAsync(default, default, default).ReturnsForAnyArgs(new List<Core.SecretsManager.Entities.Secret> { resultSecret });
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);

        if (permissionType == PermissionType.RunAsAdmin)
        {
            sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(organizationId).Returns(true);
        }
        else
        {
            resultSecret.Projects = new List<Core.SecretsManager.Entities.Project>() { mockProject };
            sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(organizationId).Returns(false);
            sutProvider.GetDependency<IProjectRepository>().UserHasReadAccessToProject(mockProject.Id, userId).Returns(true);
        }


        var result = await sutProvider.Sut.ListByOrganizationAsync(resultSecret.OrganizationId);

        await sutProvider.GetDependency<ISecretRepository>().Received(1)
            .GetManyByOrganizationIdAsync(Arg.Is(AssertHelper.AssertPropertyEqual(resultSecret.OrganizationId)), userId, accessType);
    }

    [Theory]
    [BitAutoData]
    public async void GetSecretsByOrganization_AccessDenied_Throws(SutProvider<SecretsController> sutProvider, Core.SecretsManager.Entities.Secret resultSecret)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(default).ReturnsForAnyArgs(false);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.ListByOrganizationAsync(resultSecret.OrganizationId));
    }

    [Theory]
    [BitAutoData]
    public async void GetSecret_NotFound(SutProvider<SecretsController> sutProvider)
    {
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetAsync(Guid.NewGuid()));
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsAdmin)]
    [BitAutoData(PermissionType.RunAsUserWithPermission)]
    public async void GetSecret_Success(PermissionType permissionType, SutProvider<SecretsController> sutProvider, Secret resultSecret, Guid userId, Guid organizationId, Project mockProject)
    {
        sutProvider.GetDependency<ISecretRepository>().GetByIdAsync(default).ReturnsForAnyArgs(resultSecret);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);

        if (permissionType == PermissionType.RunAsAdmin)
        {
            resultSecret.OrganizationId = organizationId;
            sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(organizationId).Returns(true);
        }
        else
        {
            resultSecret.Projects = new List<Core.SecretsManager.Entities.Project>() { mockProject };
            sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(organizationId).Returns(false);
            sutProvider.GetDependency<IProjectRepository>().UserHasReadAccessToProject(mockProject.Id, userId).Returns(true);
        }


        var result = await sutProvider.Sut.GetAsync(resultSecret.Id);

        await sutProvider.GetDependency<ISecretRepository>().Received(1)
                     .GetByIdAsync(Arg.Is(AssertHelper.AssertPropertyEqual(resultSecret.Id)));
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsAdmin)]
    [BitAutoData(PermissionType.RunAsUserWithPermission)]
    public async void CreateSecret_Success(PermissionType permissionType, SutProvider<SecretsController> sutProvider, SecretCreateRequestModel data, Guid organizationId, Project mockProject, Guid userId)
    {
        var resultSecret = data.ToSecret(organizationId);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);

        if (permissionType == PermissionType.RunAsAdmin)
        {
            sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(organizationId).Returns(true);
        }
        else
        {
            resultSecret.Projects = new List<Core.SecretsManager.Entities.Project>() { mockProject };
            sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(organizationId).Returns(false);
            sutProvider.GetDependency<IProjectRepository>().UserHasReadAccessToProject(mockProject.Id, userId).Returns(true);
        }

        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(organizationId).Returns(true);
        sutProvider.GetDependency<ICreateSecretCommand>().CreateAsync(default, userId).ReturnsForAnyArgs(resultSecret);

        var result = await sutProvider.Sut.CreateAsync(organizationId, data);
        await sutProvider.GetDependency<ICreateSecretCommand>().Received(1)
                     .CreateAsync(Arg.Any<Secret>(), userId);
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsAdmin)]
    [BitAutoData(PermissionType.RunAsUserWithPermission)]
    public async void UpdateSecret_Success(PermissionType permissionType, SutProvider<SecretsController> sutProvider, SecretUpdateRequestModel data, Guid secretId, Guid organizationId, Guid userId, Project mockProject)
    {
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);

        if (permissionType == PermissionType.RunAsAdmin)
        {
            sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(organizationId).Returns(true);
        }
        else
        {
            data.ProjectIds = new Guid[] { mockProject.Id };
            sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(organizationId).Returns(false);
            sutProvider.GetDependency<IProjectRepository>().UserHasReadAccessToProject(mockProject.Id, userId).Returns(true);
        }

        var resultSecret = data.ToSecret(secretId);
        sutProvider.GetDependency<IUpdateSecretCommand>().UpdateAsync(default, userId).ReturnsForAnyArgs(resultSecret);

        var result = await sutProvider.Sut.UpdateSecretAsync(secretId, data);
        await sutProvider.GetDependency<IUpdateSecretCommand>().Received(1)
                     .UpdateAsync(Arg.Any<Secret>(), userId);
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsAdmin)]
    [BitAutoData(PermissionType.RunAsUserWithPermission)]
    public async void BulkDeleteSecret_Success(PermissionType permissionType, SutProvider<SecretsController> sutProvider, List<Secret> data, Guid organizationId, Guid userId, Project mockProject)
    {
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);

        if (permissionType == PermissionType.RunAsAdmin)
        {
            sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(organizationId).Returns(true);
        }
        else
        {
            data.FirstOrDefault().Projects = new List<Project>() { mockProject };
            sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(organizationId).Returns(false);
            sutProvider.GetDependency<IProjectRepository>().UserHasReadAccessToProject(mockProject.Id, userId).Returns(true);
        }


        var ids = data.Select(secret => secret.Id).ToList();
        var mockResult = new List<Tuple<Secret, string>>();

        foreach (var secret in data)
        {
            mockResult.Add(new Tuple<Secret, string>(secret, ""));
        }
        sutProvider.GetDependency<IDeleteSecretCommand>().DeleteSecrets(ids, userId, organizationId).ReturnsForAnyArgs(mockResult);

        var results = await sutProvider.Sut.BulkDeleteAsync(ids, organizationId);
        await sutProvider.GetDependency<IDeleteSecretCommand>().Received(1)
                     .DeleteSecrets(Arg.Is(ids), userId, organizationId);
        Assert.Equal(data.Count, results.Data.Count());
    }

    [Theory]
    [BitAutoData]
    public async void BulkDeleteSecret_NoGuids_ThrowsArgumentNullException(SutProvider<SecretsController> sutProvider, Guid organizationId)
    {
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(new Guid());
        await Assert.ThrowsAsync<ArgumentNullException>(() => sutProvider.Sut.BulkDeleteAsync(new List<Guid>(), organizationId));
    }
}
