using System.Security.Claims;
using Bit.Api.SecretsManager.Controllers;
using Bit.Api.SecretsManager.Models.Request;
using Bit.Api.Test.SecretsManager.Enums;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Commands.Secrets.Interfaces;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Models.Data;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.SecretsManager.AutoFixture.SecretsFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using Xunit;

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
        sutProvider.GetDependency<ISecretRepository>().GetManyByOrganizationIdAsync(default, default, default)
            .ReturnsForAnyArgs(new List<SecretPermissionDetails>
            {
                new() { Secret = resultSecret, Read = true, Write = true },
            });
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);

        if (permissionType == PermissionType.RunAsAdmin)
        {
            sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(organizationId).Returns(true);
        }
        else
        {
            resultSecret.Projects = new List<Core.SecretsManager.Entities.Project>() { mockProject };
            sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(organizationId).Returns(false);
            sutProvider.GetDependency<IProjectRepository>().AccessToProjectAsync(default, default, default)
                .Returns((true, true));
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
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(organizationId).Returns(true);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        mockProject.OrganizationId = organizationId;
        resultSecret.Projects = new List<Project>() { mockProject };
        resultSecret.OrganizationId = organizationId;

        sutProvider.GetDependency<ISecretRepository>().GetByIdAsync(default).ReturnsForAnyArgs(resultSecret);
        sutProvider.GetDependency<ISecretRepository>().AccessToSecretAsync(default, default, default)
            .ReturnsForAnyArgs(Task.FromResult((true, true)));

        if (permissionType == PermissionType.RunAsAdmin)
        {
            resultSecret.OrganizationId = organizationId;
            sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(organizationId).Returns(true);
            sutProvider.GetDependency<IProjectRepository>().AccessToProjectAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), AccessClientType.NoAccessCheck)
                .Returns((true, true));
        }
        else
        {
            sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(organizationId).Returns(false);
            sutProvider.GetDependency<IProjectRepository>().AccessToProjectAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), AccessClientType.User)
                .Returns((true, true));
        }

        await sutProvider.Sut.GetAsync(resultSecret.Id);

        await sutProvider.GetDependency<ISecretRepository>().Received(1)
                     .GetByIdAsync(Arg.Is(AssertHelper.AssertPropertyEqual(resultSecret.Id)));
    }

    [Theory]
    [BitAutoData]
    public async void CreateSecret_NoAccess_Throws(SutProvider<SecretsController> sutProvider, SecretCreateRequestModel data, Guid organizationId, Guid userId)
    {
        // We currently only allow a secret to be in one project at a time
        if (data.ProjectIds != null && data.ProjectIds.Length > 1)
        {
            data.ProjectIds = new Guid[] { data.ProjectIds.ElementAt(0) };
        }

        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), data.ToSecret(organizationId),
                Arg.Any<IEnumerable<IAuthorizationRequirement>>()).ReturnsForAnyArgs(AuthorizationResult.Failed());

        var resultSecret = data.ToSecret(organizationId);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);

        sutProvider.GetDependency<ICreateSecretCommand>().CreateAsync(default).ReturnsForAnyArgs(resultSecret);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.CreateAsync(organizationId, data));
        await sutProvider.GetDependency<ICreateSecretCommand>().DidNotReceiveWithAnyArgs()
            .CreateAsync(Arg.Any<Secret>());
    }

    [Theory]
    [BitAutoData]
    public async void CreateSecret_Success(SutProvider<SecretsController> sutProvider, SecretCreateRequestModel data, Guid organizationId, Guid userId)
    {
        // We currently only allow a secret to be in one project at a time
        if (data.ProjectIds != null && data.ProjectIds.Length > 1)
        {
            data.ProjectIds = new Guid[] { data.ProjectIds.ElementAt(0) };
        }

        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), data.ToSecret(organizationId),
                Arg.Any<IEnumerable<IAuthorizationRequirement>>()).ReturnsForAnyArgs(AuthorizationResult.Success());

        var resultSecret = data.ToSecret(organizationId);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);

        sutProvider.GetDependency<ICreateSecretCommand>().CreateAsync(default).ReturnsForAnyArgs(resultSecret);

        await sutProvider.Sut.CreateAsync(organizationId, data);

        await sutProvider.GetDependency<ICreateSecretCommand>().Received(1)
            .CreateAsync(Arg.Any<Secret>());
    }

    [Theory]
    [BitAutoData]
    public async void UpdateSecret_NoAccess_Throws(SutProvider<SecretsController> sutProvider, SecretUpdateRequestModel data, Guid secretId, Guid organizationId, Secret mockSecret)
    {
        // We currently only allow a secret to be in one project at a time
        if (data.ProjectIds != null && data.ProjectIds.Length > 1)
        {
            data.ProjectIds = new Guid[] { data.ProjectIds.ElementAt(0) };
        }

        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), data.ToSecret(secretId, organizationId),
                Arg.Any<IEnumerable<IAuthorizationRequirement>>()).ReturnsForAnyArgs(AuthorizationResult.Failed());
        sutProvider.GetDependency<ISecretRepository>().GetByIdAsync(secretId).ReturnsForAnyArgs(mockSecret);

        var resultSecret = data.ToSecret(secretId, organizationId);
        sutProvider.GetDependency<IUpdateSecretCommand>().UpdateAsync(default).ReturnsForAnyArgs(resultSecret);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.UpdateSecretAsync(secretId, data));
        await sutProvider.GetDependency<IUpdateSecretCommand>().DidNotReceiveWithAnyArgs()
            .UpdateAsync(Arg.Any<Secret>());
    }

    [Theory]
    [BitAutoData]
    public async void UpdateSecret_SecretDoesNotExist_Throws(SutProvider<SecretsController> sutProvider, SecretUpdateRequestModel data, Guid secretId, Guid organizationId)
    {
        // We currently only allow a secret to be in one project at a time
        if (data.ProjectIds != null && data.ProjectIds.Length > 1)
        {
            data.ProjectIds = new Guid[] { data.ProjectIds.ElementAt(0) };
        }

        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), data.ToSecret(secretId, organizationId),
                Arg.Any<IEnumerable<IAuthorizationRequirement>>()).ReturnsForAnyArgs(AuthorizationResult.Failed());

        var resultSecret = data.ToSecret(secretId, organizationId);
        sutProvider.GetDependency<IUpdateSecretCommand>().UpdateAsync(default).ReturnsForAnyArgs(resultSecret);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.UpdateSecretAsync(secretId, data));
        await sutProvider.GetDependency<IUpdateSecretCommand>().DidNotReceiveWithAnyArgs()
            .UpdateAsync(Arg.Any<Secret>());
    }

    [Theory]
    [BitAutoData]
    public async void UpdateSecret_Success(SutProvider<SecretsController> sutProvider, SecretUpdateRequestModel data, Guid secretId, Guid organizationId, Secret mockSecret)
    {
        // We currently only allow a secret to be in one project at a time
        if (data.ProjectIds != null && data.ProjectIds.Length > 1)
        {
            data.ProjectIds = new Guid[] { data.ProjectIds.ElementAt(0) };
        }

        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), data.ToSecret(secretId, organizationId),
                Arg.Any<IEnumerable<IAuthorizationRequirement>>()).ReturnsForAnyArgs(AuthorizationResult.Success());
        sutProvider.GetDependency<ISecretRepository>().GetByIdAsync(secretId).ReturnsForAnyArgs(mockSecret);

        var resultSecret = data.ToSecret(secretId, organizationId);
        sutProvider.GetDependency<IUpdateSecretCommand>().UpdateAsync(default).ReturnsForAnyArgs(resultSecret);

        await sutProvider.Sut.UpdateSecretAsync(secretId, data);
        await sutProvider.GetDependency<IUpdateSecretCommand>().Received(1)
            .UpdateAsync(Arg.Any<Secret>());
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
            sutProvider.GetDependency<IProjectRepository>().AccessToProjectAsync(default, default, default)
                .Returns((true, true));
        }


        var ids = data.Select(secret => secret.Id).ToList();
        var mockResult = new List<Tuple<Secret, string>>();

        foreach (var secret in data)
        {
            mockResult.Add(new Tuple<Secret, string>(secret, ""));
        }
        sutProvider.GetDependency<IDeleteSecretCommand>().DeleteSecrets(ids, userId).ReturnsForAnyArgs(mockResult);

        var results = await sutProvider.Sut.BulkDeleteAsync(ids);
        await sutProvider.GetDependency<IDeleteSecretCommand>().Received(1)
                     .DeleteSecrets(Arg.Is(ids), userId);
        Assert.Equal(data.Count, results.Data.Count());
    }

    [Theory]
    [BitAutoData]
    public async void BulkDeleteSecret_NoGuids_ThrowsArgumentNullException(SutProvider<SecretsController> sutProvider)
    {
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(new Guid());
        await Assert.ThrowsAsync<ArgumentNullException>(() => sutProvider.Sut.BulkDeleteAsync(new List<Guid>()));
    }
}
