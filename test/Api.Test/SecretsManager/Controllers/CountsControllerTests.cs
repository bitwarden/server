using System.Security.Claims;
using Bit.Api.SecretsManager.Controllers;
using Bit.Api.SecretsManager.Models.Response;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Models.Data;
using Bit.Core.SecretsManager.Queries.Interfaces;
using Bit.Core.SecretsManager.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.SecretsManager.Controllers;

[ControllerCustomize(typeof(CountsController))]
[SutProviderCustomize]
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
    [BitAutoData(AccessClientType.NoAccessCheck)]
    [BitAutoData(AccessClientType.User)]
    [BitAutoData(AccessClientType.ServiceAccount)]
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
    public async Task GetByOrganizationAndProjectAsync_NoAccess_Throws(SutProvider<CountsController> sutProvider,
        Guid organizationId, Guid projectId)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(organizationId).Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetByOrganizationAndProjectAsync(organizationId, projectId));
    }

    [Theory]
    [BitAutoData(AccessClientType.NoAccessCheck)]
    [BitAutoData(AccessClientType.User)]
    [BitAutoData(AccessClientType.ServiceAccount)]
    public async Task GetByOrganizationAndProjectAsync_HasAccess_Success(AccessClientType accessClientType,
        SutProvider<CountsController> sutProvider, Guid organizationId, Guid userId, Guid projectId,
        ProjectCountsResponseModel expectedProjectCountsResponseModel)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(organizationId).Returns(true);

        sutProvider.GetDependency<IAccessClientQuery>()
            .GetAccessClientAsync(Arg.Any<ClaimsPrincipal>(), organizationId).Returns((accessClientType, userId));

        sutProvider.GetDependency<IProjectRepository>()
            .GetProjectCountsByIdAsync(projectId, userId, accessClientType)
            .Returns(new ProjectCounts
            {
                Secrets = expectedProjectCountsResponseModel.Secrets,
                People = expectedProjectCountsResponseModel.People,
                ServiceAccounts = expectedProjectCountsResponseModel.ServiceAccounts
            });

        var response = await sutProvider.Sut.GetByOrganizationAndProjectAsync(organizationId, projectId);

        Assert.Equal(expectedProjectCountsResponseModel.Secrets, response.Secrets);
        Assert.Equal(expectedProjectCountsResponseModel.People, response.People);
        Assert.Equal(expectedProjectCountsResponseModel.ServiceAccounts, response.ServiceAccounts);
    }

    [Theory]
    [BitAutoData]
    public async Task GetByOrganizationAndServiceAccountAsync_NoAccess_Throws(SutProvider<CountsController> sutProvider,
        Guid organizationId, Guid serviceAccountId)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(organizationId).Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetByOrganizationAndServiceAccountAsync(organizationId, serviceAccountId));
    }

    [Theory]
    [BitAutoData(AccessClientType.NoAccessCheck)]
    [BitAutoData(AccessClientType.User)]
    [BitAutoData(AccessClientType.ServiceAccount)]
    public async Task GetByOrganizationAndServiceAccountAsync_HasAccess_Success(AccessClientType accessClientType,
        SutProvider<CountsController> sutProvider, Guid organizationId, Guid userId, Guid serviceAccountId,
        ServiceAccountCountsResponseModel expectedServiceAccountCountsResponseModel)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(organizationId).Returns(true);

        sutProvider.GetDependency<IAccessClientQuery>()
            .GetAccessClientAsync(Arg.Any<ClaimsPrincipal>(), organizationId).Returns((accessClientType, userId));

        sutProvider.GetDependency<IServiceAccountRepository>()
            .GetServiceAccountCountsByIdAsync(serviceAccountId, userId, accessClientType)
            .Returns(new ServiceAccountCounts
            {
                Projects = expectedServiceAccountCountsResponseModel.Projects,
                People = expectedServiceAccountCountsResponseModel.People,
                AccessTokens = expectedServiceAccountCountsResponseModel.AccessTokens
            });

        var response = await sutProvider.Sut.GetByOrganizationAndServiceAccountAsync(organizationId, serviceAccountId);

        Assert.Equal(expectedServiceAccountCountsResponseModel.Projects, response.Projects);
        Assert.Equal(expectedServiceAccountCountsResponseModel.People, response.People);
        Assert.Equal(expectedServiceAccountCountsResponseModel.AccessTokens, response.AccessTokens);
    }
}
