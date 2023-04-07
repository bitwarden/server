using Bit.Api.SecretsManager.Controllers;
using Bit.Api.SecretsManager.Models.Request;
using Bit.Api.Test.SecretsManager.Enums;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Commands.Projects.Interfaces;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Queries.Access.Interfaces;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.SecretsManager.AutoFixture.ProjectsFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
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
            .ReturnsForAnyArgs(new List<Project> { mockProject });

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
        sutProvider.GetDependency<IProjectAccessQuery>().HasAccess(data.ToAccessCheck(orgId)).ReturnsForAnyArgs(false);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(Guid.NewGuid());

        var resultProject = data.ToProject(orgId);

        sutProvider.GetDependency<ICreateProjectCommand>().CreateAsync(default, default)
            .ReturnsForAnyArgs(resultProject);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.CreateAsync(orgId, data));
        await sutProvider.GetDependency<ICreateProjectCommand>().DidNotReceiveWithAnyArgs()
            .CreateAsync(Arg.Any<Project>(), Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData]
    public async void Create_Success(SutProvider<ProjectsController> sutProvider,
        Guid orgId, ProjectCreateRequestModel data)
    {
        sutProvider.GetDependency<IProjectAccessQuery>().HasAccess(data.ToAccessCheck(orgId)).ReturnsForAnyArgs(true);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(Guid.NewGuid());

        var resultProject = data.ToProject(orgId);

        sutProvider.GetDependency<ICreateProjectCommand>().CreateAsync(default, default)
            .ReturnsForAnyArgs(resultProject);

        await sutProvider.Sut.CreateAsync(orgId, data);

        await sutProvider.GetDependency<ICreateProjectCommand>().Received(1)
            .CreateAsync(Arg.Any<Project>(), Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData]
    public async void Update_NoAccess_Throws(SutProvider<ProjectsController> sutProvider,
        Guid userId, ProjectUpdateRequestModel data, Project existingProject)
    {
        sutProvider.GetDependency<IProjectAccessQuery>().HasAccess(data.ToAccessCheck(existingProject.OrganizationId, existingProject.Id, userId)).ReturnsForAnyArgs(false);
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
        sutProvider.GetDependency<IProjectAccessQuery>().HasAccess(data.ToAccessCheck(existingProject.OrganizationId, existingProject.Id, userId)).ReturnsForAnyArgs(true);
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
                sutProvider.GetDependency<IProjectRepository>()
                    .UserHasReadAccessToProject(Arg.Is(data), Arg.Any<Guid>()).ReturnsForAnyArgs(true);
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
        sutProvider.GetDependency<IProjectRepository>().UserHasReadAccessToProject(Arg.Is(data), Arg.Any<Guid>())
            .ReturnsForAnyArgs(false);

        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(Arg.Is(data))
            .ReturnsForAnyArgs(new Project { Id = data, OrganizationId = orgId });

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetAsync(data));
    }

    [Theory]
    [BitAutoData]
    public async void BulkDeleteProjects_Success(SutProvider<ProjectsController> sutProvider, List<Project> data)
    {
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(Guid.NewGuid());
        var ids = data.Select(project => project.Id).ToList();
        var mockResult = data.Select(project => new Tuple<Project, string>(project, "")).ToList();

        sutProvider.GetDependency<IDeleteProjectCommand>().DeleteProjects(ids, default).ReturnsForAnyArgs(mockResult);

        var results = await sutProvider.Sut.BulkDeleteAsync(ids);
        await sutProvider.GetDependency<IDeleteProjectCommand>().Received(1)
            .DeleteProjects(Arg.Is(ids), Arg.Any<Guid>());
        Assert.Equal(data.Count, results.Data.Count());
    }

    [Theory]
    [BitAutoData]
    public async void BulkDeleteProjects_NoGuids_ThrowsArgumentNullException(
        SutProvider<ProjectsController> sutProvider)
    {
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(Guid.NewGuid());
        await Assert.ThrowsAsync<ArgumentNullException>(() => sutProvider.Sut.BulkDeleteAsync(new List<Guid>()));
    }
}
