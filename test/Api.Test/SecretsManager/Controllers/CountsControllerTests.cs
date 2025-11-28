#nullable enable
using System.Security.Claims;
using Bit.Api.SecretsManager.Controllers;
using Bit.Api.SecretsManager.Models.Response;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Models.Data;
using Bit.Core.SecretsManager.Queries.Interfaces;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Test.SecretsManager.AutoFixture.ProjectsFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.SecretsManager.Controllers;

[ControllerCustomize(typeof(CountsController))]
[SutProviderCustomize]
[ProjectCustomize]
[JsonDocumentCustomize]
public class CountsControllerTests
{
    [Theory]
    [BitAutoData]
    public async Task GetByOrganizationAsync_NoAccess_Throws(SutProvider<CountsController> sutProvider,
        Guid organizationId)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(organizationId).Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetByOrganizationAsync(organizationId));
    }

    [Theory]
    [BitAutoData]
    public async Task GetByOrganizationAsync_ServiceAccountAccess_Throws(SutProvider<CountsController> sutProvider,
        Guid organizationId, Guid userId)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(organizationId).Returns(true);

        sutProvider.GetDependency<IAccessClientQuery>()
            .GetAccessClientAsync(Arg.Any<ClaimsPrincipal>(), organizationId)
            .Returns((AccessClientType.ServiceAccount, userId));

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetByOrganizationAsync(organizationId));
    }

    [Theory]
    [BitAutoData(AccessClientType.NoAccessCheck)]
    [BitAutoData(AccessClientType.User)]
    public async Task GetByOrganizationAsync_HasAccess_Success(AccessClientType accessClientType,
        SutProvider<CountsController> sutProvider, Guid organizationId, Guid userId,
        OrganizationCountsResponseModel expectedCountsResponseModel)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(organizationId).Returns(true);

        sutProvider.GetDependency<IAccessClientQuery>()
            .GetAccessClientAsync(Arg.Any<ClaimsPrincipal>(), organizationId).Returns((accessClientType, userId));

        sutProvider.GetDependency<IProjectRepository>()
            .GetProjectCountByOrganizationIdAsync(organizationId, userId, accessClientType)
            .Returns(expectedCountsResponseModel.Projects);

        sutProvider.GetDependency<ISecretRepository>()
            .GetSecretsCountByOrganizationIdAsync(organizationId, userId, accessClientType)
            .Returns(expectedCountsResponseModel.Secrets);

        sutProvider.GetDependency<IServiceAccountRepository>()
            .GetServiceAccountCountByOrganizationIdAsync(organizationId, userId, accessClientType)
            .Returns(expectedCountsResponseModel.ServiceAccounts);

        var response = await sutProvider.Sut.GetByOrganizationAsync(organizationId);

        Assert.Equal(expectedCountsResponseModel.Projects, response.Projects);
        Assert.Equal(expectedCountsResponseModel.Secrets, response.Secrets);
        Assert.Equal(expectedCountsResponseModel.ServiceAccounts, response.ServiceAccounts);
    }

    [Theory]
    [BitAutoData]
    public async Task GetByProjectAsync_ProjectNotFound_Throws(SutProvider<CountsController> sutProvider,
        Guid projectId)
    {
        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(projectId).Returns(default(Project));

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetByProjectAsync(projectId));
    }

    [Theory]
    [BitAutoData]
    public async Task GetByProjectAsync_NoAccess_Throws(SutProvider<CountsController> sutProvider, Project project)
    {
        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(project.Id).Returns(project);
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(project.OrganizationId).Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetByProjectAsync(project.Id));
    }

    [Theory]
    [BitAutoData]
    public async Task GetByProjectAsync_ServiceAccountAccess_Throws(SutProvider<CountsController> sutProvider,
        Guid userId, Project project)
    {
        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(project.Id).Returns(project);
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(project.OrganizationId).Returns(true);

        sutProvider.GetDependency<IAccessClientQuery>()
            .GetAccessClientAsync(Arg.Any<ClaimsPrincipal>(), project.OrganizationId)
            .Returns((AccessClientType.ServiceAccount, userId));

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetByProjectAsync(project.Id));
    }

    [Theory]
    [BitAutoData(AccessClientType.NoAccessCheck)]
    [BitAutoData(AccessClientType.User)]
    public async Task GetByProjectAsync_HasAccess_Success(AccessClientType accessClientType,
        SutProvider<CountsController> sutProvider, Guid userId, Project project,
        ProjectCountsResponseModel expectedProjectCountsResponseModel)
    {
        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(project.Id).Returns(project);
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(project.OrganizationId).Returns(true);
        sutProvider.GetDependency<IAccessClientQuery>()
            .GetAccessClientAsync(Arg.Any<ClaimsPrincipal>(), project.OrganizationId)
            .Returns((accessClientType, userId));

        sutProvider.GetDependency<IProjectRepository>()
            .GetProjectCountsByIdAsync(project.Id, userId, accessClientType)
            .Returns(new ProjectCounts
            {
                Secrets = expectedProjectCountsResponseModel.Secrets,
                People = expectedProjectCountsResponseModel.People,
                ServiceAccounts = expectedProjectCountsResponseModel.ServiceAccounts
            });

        var response = await sutProvider.Sut.GetByProjectAsync(project.Id);

        Assert.Equal(expectedProjectCountsResponseModel.Secrets, response.Secrets);
        Assert.Equal(expectedProjectCountsResponseModel.People, response.People);
        Assert.Equal(expectedProjectCountsResponseModel.ServiceAccounts, response.ServiceAccounts);
    }

    [Theory]
    [BitAutoData]
    public async Task GetByServiceAccountAsync_ServiceAccountNotFound_Throws(SutProvider<CountsController> sutProvider,
        Guid serviceAccountId)
    {
        sutProvider.GetDependency<IServiceAccountRepository>().GetByIdAsync(serviceAccountId)
            .Returns(default(ServiceAccount));

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetByServiceAccountAsync(serviceAccountId));
    }

    [Theory]
    [BitAutoData]
    public async Task GetByServiceAccountAsync_NoAccess_Throws(SutProvider<CountsController> sutProvider,
        ServiceAccount serviceAccount)
    {
        sutProvider.GetDependency<IServiceAccountRepository>().GetByIdAsync(serviceAccount.Id)
            .Returns(serviceAccount);
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(serviceAccount.OrganizationId).Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetByServiceAccountAsync(serviceAccount.Id));
    }

    [Theory]
    [BitAutoData]
    public async Task GetByServiceAccountAsync_ServiceAccountAccess_Throws(SutProvider<CountsController> sutProvider,
        Guid userId, ServiceAccount serviceAccount)
    {
        sutProvider.GetDependency<IServiceAccountRepository>().GetByIdAsync(serviceAccount.Id).Returns(serviceAccount);
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(serviceAccount.OrganizationId).Returns(true);

        sutProvider.GetDependency<IAccessClientQuery>()
            .GetAccessClientAsync(Arg.Any<ClaimsPrincipal>(), serviceAccount.OrganizationId)
            .Returns((AccessClientType.ServiceAccount, userId));

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetByServiceAccountAsync(serviceAccount.Id));
    }

    [Theory]
    [BitAutoData(AccessClientType.NoAccessCheck)]
    [BitAutoData(AccessClientType.User)]
    public async Task GetByServiceAccountAsync_HasAccess_Success(AccessClientType accessClientType,
        SutProvider<CountsController> sutProvider, Guid userId, ServiceAccount serviceAccount,
        ServiceAccountCountsResponseModel expectedServiceAccountCountsResponseModel)
    {
        sutProvider.GetDependency<IServiceAccountRepository>().GetByIdAsync(serviceAccount.Id)
            .Returns(serviceAccount);
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(serviceAccount.OrganizationId).Returns(true);
        sutProvider.GetDependency<IAccessClientQuery>()
            .GetAccessClientAsync(Arg.Any<ClaimsPrincipal>(), serviceAccount.OrganizationId)
            .Returns((accessClientType, userId));

        sutProvider.GetDependency<IServiceAccountRepository>()
            .GetServiceAccountCountsByIdAsync(serviceAccount.Id, userId, accessClientType)
            .Returns(new ServiceAccountCounts
            {
                Projects = expectedServiceAccountCountsResponseModel.Projects,
                People = expectedServiceAccountCountsResponseModel.People,
                AccessTokens = expectedServiceAccountCountsResponseModel.AccessTokens
            });

        var response = await sutProvider.Sut.GetByServiceAccountAsync(serviceAccount.Id);

        Assert.Equal(expectedServiceAccountCountsResponseModel.Projects, response.Projects);
        Assert.Equal(expectedServiceAccountCountsResponseModel.People, response.People);
        Assert.Equal(expectedServiceAccountCountsResponseModel.AccessTokens, response.AccessTokens);
    }
}
