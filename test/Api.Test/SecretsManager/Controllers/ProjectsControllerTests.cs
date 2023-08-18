using System.Security.Claims;
using Bit.Api.SecretsManager.Controllers;
using Bit.Api.SecretsManager.Models.Request;
using Bit.Api.Test.SecretsManager.Enums;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Commands.Projects.Interfaces;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Models.Data;
using Bit.Core.SecretsManager.Queries.Projects.Interfaces;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.SecretsManager.AutoFixture.ProjectsFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.SecretsManager.Controllers;

[ControllerCustomize(typeof(ProjectsController))]
[SutProviderCustomize]
[ProjectCustomize]
[JsonDocumentCustomize]
public class ProjectsControllerTests
{
    private static void SetupAdmin(SutProvider<ProjectsController> sutProvider, Guid organizationId)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(default).ReturnsForAnyArgs(true);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(Guid.NewGuid());
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(organizationId).Returns(true);
    }

    private static void SetupUserWithPermission(SutProvider<ProjectsController> sutProvider, Guid organizationId)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(default).ReturnsForAnyArgs(true);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(Guid.NewGuid());
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(organizationId).Returns(false);
        sutProvider.GetDependency<ICurrentContext>().OrganizationUser(default).ReturnsForAnyArgs(true);
    }

    [Theory]
    [BitAutoData]
    public async void ListByOrganization_SmNotEnabled_Throws(SutProvider<ProjectsController> sutProvider, Guid data)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(data).Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.ListByOrganizationAsync(data));
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsAdmin)]
    [BitAutoData(PermissionType.RunAsUserWithPermission)]
    public async void ListByOrganization_ReturnsEmptyList(PermissionType permissionType,
        SutProvider<ProjectsController> sutProvider, Guid data)
    {
        switch (permissionType)
        {
            case PermissionType.RunAsAdmin:
                SetupAdmin(sutProvider, data);
                break;
            case PermissionType.RunAsUserWithPermission:
                SetupUserWithPermission(sutProvider, data);
                break;
        }

        var result = await sutProvider.Sut.ListByOrganizationAsync(data);

        await sutProvider.GetDependency<IProjectRepository>().Received(1)
            .GetManyByOrganizationIdAsync(Arg.Is(AssertHelper.AssertPropertyEqual(data)), Arg.Any<Guid>(),
                Arg.Any<AccessClientType>());
        Assert.Empty(result.Data);
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsAdmin)]
    [BitAutoData(PermissionType.RunAsUserWithPermission)]
    public async void ListByOrganization_Success(PermissionType permissionType,
        SutProvider<ProjectsController> sutProvider, Guid data, Project mockProject)
    {
        switch (permissionType)
        {
            case PermissionType.RunAsAdmin:
                SetupAdmin(sutProvider, data);
                break;
            case PermissionType.RunAsUserWithPermission:
                SetupUserWithPermission(sutProvider, data);
                break;
        }

        sutProvider.GetDependency<IProjectRepository>().GetManyByOrganizationIdAsync(default, default, default)
            .ReturnsForAnyArgs(new List<ProjectPermissionDetails> { new() { Project = mockProject, Read = true, Write = true } });

        var result = await sutProvider.Sut.ListByOrganizationAsync(data);

        await sutProvider.GetDependency<IProjectRepository>().Received(1)
            .GetManyByOrganizationIdAsync(Arg.Is(AssertHelper.AssertPropertyEqual(data)), Arg.Any<Guid>(),
                Arg.Any<AccessClientType>());
        Assert.NotEmpty(result.Data);
        Assert.Single(result.Data);
    }

    [Theory]
    [BitAutoData]
    public async void Create_NoAccess_Throws(SutProvider<ProjectsController> sutProvider,
        Guid orgId, ProjectCreateRequestModel data)
    {
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), data.ToProject(orgId),
                Arg.Any<IEnumerable<IAuthorizationRequirement>>()).ReturnsForAnyArgs(AuthorizationResult.Failed());
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(Guid.NewGuid());

        var resultProject = data.ToProject(orgId);

        sutProvider.GetDependency<ICreateProjectCommand>().CreateAsync(default, default, sutProvider.GetDependency<ICurrentContext>().ClientType)
            .ReturnsForAnyArgs(resultProject);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.CreateAsync(orgId, data));
        await sutProvider.GetDependency<ICreateProjectCommand>().DidNotReceiveWithAnyArgs()
            .CreateAsync(Arg.Any<Project>(), Arg.Any<Guid>(), sutProvider.GetDependency<ICurrentContext>().ClientType);
    }

    [Theory]
    [BitAutoData]
    public async void Create_OverProjectLimit_Throws(SutProvider<ProjectsController> sutProvider,
        Guid orgId, ProjectCreateRequestModel data)
    {
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), data.ToProject(orgId),
                Arg.Any<IEnumerable<IAuthorizationRequirement>>()).ReturnsForAnyArgs(AuthorizationResult.Success());
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(Guid.NewGuid());
        sutProvider.GetDependency<IMaxProjectsQuery>().GetByOrgIdAsync(orgId).Returns(((short)3, true));


        await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.CreateAsync(orgId, data));

        await sutProvider.GetDependency<ICreateProjectCommand>().DidNotReceiveWithAnyArgs()
            .CreateAsync(Arg.Any<Project>(), Arg.Any<Guid>(), sutProvider.GetDependency<ICurrentContext>().ClientType);
    }

    [Theory]
    [BitAutoData]
    public async void Create_Success(SutProvider<ProjectsController> sutProvider,
        Guid orgId, ProjectCreateRequestModel data)
    {
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), data.ToProject(orgId),
                Arg.Any<IEnumerable<IAuthorizationRequirement>>()).ReturnsForAnyArgs(AuthorizationResult.Success());
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(Guid.NewGuid());

        var resultProject = data.ToProject(orgId);

        sutProvider.GetDependency<ICreateProjectCommand>().CreateAsync(default, default, sutProvider.GetDependency<ICurrentContext>().ClientType)
            .ReturnsForAnyArgs(resultProject);

        await sutProvider.Sut.CreateAsync(orgId, data);

        await sutProvider.GetDependency<ICreateProjectCommand>().Received(1)
            .CreateAsync(Arg.Any<Project>(), Arg.Any<Guid>(), sutProvider.GetDependency<ICurrentContext>().ClientType);
    }

    [Theory]
    [BitAutoData]
    public async void Update_NoAccess_Throws(SutProvider<ProjectsController> sutProvider,
        Guid userId, ProjectUpdateRequestModel data, Project existingProject)
    {
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), data.ToProject(existingProject.Id),
                Arg.Any<IEnumerable<IAuthorizationRequirement>>()).ReturnsForAnyArgs(AuthorizationResult.Failed());
        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(existingProject.Id).ReturnsForAnyArgs(existingProject);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);

        var resultProject = data.ToProject(existingProject.Id);
        sutProvider.GetDependency<IUpdateProjectCommand>().UpdateAsync(default)
            .ReturnsForAnyArgs(resultProject);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.UpdateAsync(existingProject.Id, data));
        await sutProvider.GetDependency<IUpdateProjectCommand>().DidNotReceiveWithAnyArgs()
            .UpdateAsync(Arg.Any<Project>());
    }

    [Theory]
    [BitAutoData]
    public async void Update_Success(SutProvider<ProjectsController> sutProvider,
        Guid userId, ProjectUpdateRequestModel data, Project existingProject)
    {
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), data.ToProject(existingProject.Id),
                Arg.Any<IEnumerable<IAuthorizationRequirement>>()).ReturnsForAnyArgs(AuthorizationResult.Success());
        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(existingProject.Id).ReturnsForAnyArgs(existingProject);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);

        var resultProject = data.ToProject(existingProject.Id);
        sutProvider.GetDependency<IUpdateProjectCommand>().UpdateAsync(default)
            .ReturnsForAnyArgs(resultProject);

        await sutProvider.Sut.UpdateAsync(existingProject.Id, data);

        await sutProvider.GetDependency<IUpdateProjectCommand>().Received(1)
            .UpdateAsync(Arg.Any<Project>());
    }

    [Theory]
    [BitAutoData]
    public async void Get_SmNotEnabled_Throws(SutProvider<ProjectsController> sutProvider, Guid data, Guid orgId)
    {
        SetupAdmin(sutProvider, orgId);
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(orgId).Returns(false);
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetAsync(data));
    }

    [Theory]
    [BitAutoData]
    public async void Get_ThrowsNotFound(SutProvider<ProjectsController> sutProvider, Guid data, Guid orgId)
    {
        SetupAdmin(sutProvider, orgId);
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetAsync(data));
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsAdmin)]
    [BitAutoData(PermissionType.RunAsUserWithPermission)]
    public async void Get_Success(PermissionType permissionType, SutProvider<ProjectsController> sutProvider,
        Guid orgId, Guid data)
    {
        switch (permissionType)
        {
            case PermissionType.RunAsAdmin:
                SetupAdmin(sutProvider, orgId);
                break;
            case PermissionType.RunAsUserWithPermission:
                SetupUserWithPermission(sutProvider, orgId);
                sutProvider.GetDependency<IProjectRepository>().AccessToProjectAsync(default, default, default)
                    .Returns((true, true));
                break;
        }

        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(Arg.Is(data))
            .ReturnsForAnyArgs(new Project { Id = data, OrganizationId = orgId });

        sutProvider.GetDependency<IProjectRepository>().AccessToProjectAsync(default, default, default)
            .ReturnsForAnyArgs((true, false));

        await sutProvider.Sut.GetAsync(data);

        await sutProvider.GetDependency<IProjectRepository>().Received(1)
            .GetByIdAsync(Arg.Is(data));
    }

    [Theory]
    [BitAutoData]
    public async void Get_UserWithoutPermission_Throws(SutProvider<ProjectsController> sutProvider, Guid orgId,
        Guid data)
    {
        SetupUserWithPermission(sutProvider, orgId);
        sutProvider.GetDependency<IProjectRepository>().AccessToProjectAsync(default, default, default)
            .Returns((false, false));

        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(Arg.Is(data))
            .ReturnsForAnyArgs(new Project { Id = data, OrganizationId = orgId });

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetAsync(data));
    }

    [Theory]
    [BitAutoData]
    public async void BulkDeleteProjects_NoProjectsFound_ThrowsNotFound(
        SutProvider<ProjectsController> sutProvider, List<Project> data)
    {
        var ids = data.Select(project => project.Id).ToList();
        sutProvider.GetDependency<IProjectRepository>().GetManyWithSecretsByIds(Arg.Is(ids)).ReturnsForAnyArgs(new List<Project>());
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.BulkDeleteAsync(ids));
    }

    [Theory]
    [BitAutoData]
    public async void BulkDeleteProjects_ProjectsFoundMisMatch_ThrowsNotFound(
        SutProvider<ProjectsController> sutProvider, List<Project> data, Project mockProject)
    {
        data.Add(mockProject);
        var ids = data.Select(project => project.Id).ToList();
        sutProvider.GetDependency<IProjectRepository>().GetManyWithSecretsByIds(Arg.Is(ids)).ReturnsForAnyArgs(new List<Project> { mockProject });
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.BulkDeleteAsync(ids));
    }

    [Theory]
    [BitAutoData]
    public async void BulkDeleteProjects_OrganizationMistMatch_ThrowsNotFound(
        SutProvider<ProjectsController> sutProvider, List<Project> data)
    {

        var ids = data.Select(project => project.Id).ToList();
        sutProvider.GetDependency<IProjectRepository>().GetManyWithSecretsByIds(Arg.Is(ids)).ReturnsForAnyArgs(data);
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.BulkDeleteAsync(ids));
    }

    [Theory]
    [BitAutoData]
    public async void BulkDeleteProjects_NoAccessToSecretsManager_ThrowsNotFound(
        SutProvider<ProjectsController> sutProvider, List<Project> data)
    {

        var ids = data.Select(project => project.Id).ToList();
        var organizationId = data.First().OrganizationId;
        foreach (var project in data)
        {
            project.OrganizationId = organizationId;
        }
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(Arg.Is(organizationId)).ReturnsForAnyArgs(false);
        sutProvider.GetDependency<IProjectRepository>().GetManyWithSecretsByIds(Arg.Is(ids)).ReturnsForAnyArgs(data);
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.BulkDeleteAsync(ids));
    }

    [Theory]
    [BitAutoData]
    public async void BulkDeleteProjects_ReturnsAccessDeniedForProjectsWithoutAccess_Success(
        SutProvider<ProjectsController> sutProvider, List<Project> data)
    {

        var ids = data.Select(project => project.Id).ToList();
        var organizationId = data.First().OrganizationId;
        foreach (var project in data)
        {
            project.OrganizationId = organizationId;
            sutProvider.GetDependency<IAuthorizationService>()
                .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), project,
                    Arg.Any<IEnumerable<IAuthorizationRequirement>>()).ReturnsForAnyArgs(AuthorizationResult.Success());
        }
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), data.First(),
                Arg.Any<IEnumerable<IAuthorizationRequirement>>()).Returns(AuthorizationResult.Failed());

        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(Arg.Is(organizationId)).ReturnsForAnyArgs(true);
        sutProvider.GetDependency<IProjectRepository>().GetManyWithSecretsByIds(Arg.Is(ids)).ReturnsForAnyArgs(data);
        var results = await sutProvider.Sut.BulkDeleteAsync(ids);
        Assert.Equal(data.Count, results.Data.Count());
        Assert.Equal("access denied", results.Data.First().Error);

        data.Remove(data.First());
        await sutProvider.GetDependency<IDeleteProjectCommand>().Received(1)
            .DeleteProjects(Arg.Is(AssertHelper.AssertPropertyEqual(data)));
    }

    [Theory]
    [BitAutoData]
    public async void BulkDeleteProjects_Success(SutProvider<ProjectsController> sutProvider, List<Project> data)
    {
        var ids = data.Select(project => project.Id).ToList();
        var organizationId = data.First().OrganizationId;
        foreach (var project in data)
        {
            project.OrganizationId = organizationId;
            sutProvider.GetDependency<IAuthorizationService>()
                .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), project,
                    Arg.Any<IEnumerable<IAuthorizationRequirement>>()).ReturnsForAnyArgs(AuthorizationResult.Success());
        }

        sutProvider.GetDependency<IProjectRepository>().GetManyWithSecretsByIds(Arg.Is(ids)).ReturnsForAnyArgs(data);
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(Arg.Is(organizationId)).ReturnsForAnyArgs(true);

        var results = await sutProvider.Sut.BulkDeleteAsync(ids);
        await sutProvider.GetDependency<IDeleteProjectCommand>().Received(1)
            .DeleteProjects(Arg.Is(AssertHelper.AssertPropertyEqual(data)));
        Assert.Equal(data.Count, results.Data.Count());
        foreach (var result in results.Data)
        {
            Assert.Null(result.Error);
        }
    }
}
