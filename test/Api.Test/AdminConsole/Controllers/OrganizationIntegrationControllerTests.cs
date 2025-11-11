using Bit.Api.AdminConsole.Controllers;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.AdminConsole.Models.Response.Organizations;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Controllers;

[ControllerCustomize(typeof(OrganizationIntegrationController))]
[SutProviderCustomize]
public class OrganizationIntegrationControllerTests
{
    private OrganizationIntegrationRequestModel _webhookRequestModel = new OrganizationIntegrationRequestModel()
    {
        Configuration = null,
        Type = IntegrationType.Webhook
    };

    [Theory, BitAutoData]
    public async Task GetAsync_UserIsNotOrganizationAdmin_ThrowsNotFound(
        SutProvider<OrganizationIntegrationController> sutProvider,
        Guid organizationId)
    {
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetAsync(organizationId));
    }

    [Theory, BitAutoData]
    public async Task GetAsync_IntegrationsExist_ReturnsIntegrations(
        SutProvider<OrganizationIntegrationController> sutProvider,
        Guid organizationId,
        List<OrganizationIntegration> integrations)
    {
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetManyByOrganizationAsync(organizationId)
            .Returns(integrations);

        var result = await sutProvider.Sut.GetAsync(organizationId);

        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().Received(1)
            .GetManyByOrganizationAsync(organizationId);

        Assert.Equal(integrations.Count, result.Count);
        Assert.All(result, r => Assert.IsType<OrganizationIntegrationResponseModel>(r));
    }

    [Theory, BitAutoData]
    public async Task GetAsync_NoIntegrations_ReturnsEmptyList(
        SutProvider<OrganizationIntegrationController> sutProvider,
        Guid organizationId)
    {
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetManyByOrganizationAsync(organizationId)
            .Returns([]);

        var result = await sutProvider.Sut.GetAsync(organizationId);

        Assert.Empty(result);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_Webhook_AllParamsProvided_Succeeds(
        SutProvider<OrganizationIntegrationController> sutProvider,
        Guid organizationId)
    {
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .CreateAsync(Arg.Any<OrganizationIntegration>())
            .Returns(callInfo => callInfo.Arg<OrganizationIntegration>());
        var response = await sutProvider.Sut.CreateAsync(organizationId, _webhookRequestModel);

        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().Received(1)
            .CreateAsync(Arg.Any<OrganizationIntegration>());
        Assert.IsType<OrganizationIntegrationResponseModel>(response);
        Assert.Equal(IntegrationType.Webhook, response.Type);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_UserIsNotOrganizationAdmin_ThrowsNotFound(SutProvider<OrganizationIntegrationController> sutProvider, Guid organizationId)
    {
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.CreateAsync(organizationId, _webhookRequestModel));
    }

    [Theory, BitAutoData]
    public async Task DeleteAsync_AllParamsProvided_Succeeds(
        SutProvider<OrganizationIntegrationController> sutProvider,
        Guid organizationId,
        OrganizationIntegration organizationIntegration)
    {
        organizationIntegration.OrganizationId = organizationId;
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(organizationIntegration);

        await sutProvider.Sut.DeleteAsync(organizationId, organizationIntegration.Id);

        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().Received(1)
            .GetByIdAsync(organizationIntegration.Id);
        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().Received(1)
            .DeleteAsync(organizationIntegration);
    }

    [Theory, BitAutoData]
    public async Task PostDeleteAsync_AllParamsProvided_Succeeds(
        SutProvider<OrganizationIntegrationController> sutProvider,
        Guid organizationId,
        OrganizationIntegration organizationIntegration)
    {
        organizationIntegration.OrganizationId = organizationId;
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(organizationIntegration);

        await sutProvider.Sut.PostDeleteAsync(organizationId, organizationIntegration.Id);

        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().Received(1)
            .GetByIdAsync(organizationIntegration.Id);
        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().Received(1)
            .DeleteAsync(organizationIntegration);
    }

    [Theory, BitAutoData]
    public async Task DeleteAsync_IntegrationDoesNotBelongToOrganization_ThrowsNotFound(
        SutProvider<OrganizationIntegrationController> sutProvider,
        Guid organizationId,
        OrganizationIntegration organizationIntegration)
    {
        organizationIntegration.OrganizationId = Guid.NewGuid();
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .ReturnsNull();

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.DeleteAsync(organizationId, Guid.Empty));
    }

    [Theory, BitAutoData]
    public async Task DeleteAsync_IntegrationDoesNotExist_ThrowsNotFound(
        SutProvider<OrganizationIntegrationController> sutProvider,
        Guid organizationId)
    {
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .ReturnsNull();

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.DeleteAsync(organizationId, Guid.Empty));
    }

    [Theory, BitAutoData]
    public async Task DeleteAsync_UserIsNotOrganizationAdmin_ThrowsNotFound(
        SutProvider<OrganizationIntegrationController> sutProvider,
        Guid organizationId)
    {
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.DeleteAsync(organizationId, Guid.Empty));
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_AllParamsProvided_Succeeds(
        SutProvider<OrganizationIntegrationController> sutProvider,
        Guid organizationId,
        OrganizationIntegration organizationIntegration)
    {
        organizationIntegration.OrganizationId = organizationId;
        organizationIntegration.Type = IntegrationType.Webhook;
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(organizationIntegration);

        var response = await sutProvider.Sut.UpdateAsync(organizationId, organizationIntegration.Id, _webhookRequestModel);

        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().Received(1)
            .GetByIdAsync(organizationIntegration.Id);
        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().Received(1)
            .ReplaceAsync(organizationIntegration);
        Assert.IsType<OrganizationIntegrationResponseModel>(response);
        Assert.Equal(IntegrationType.Webhook, response.Type);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_IntegrationDoesNotBelongToOrganization_ThrowsNotFound(
        SutProvider<OrganizationIntegrationController> sutProvider,
        Guid organizationId,
        OrganizationIntegration organizationIntegration)
    {
        organizationIntegration.OrganizationId = Guid.NewGuid();
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .ReturnsNull();

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.UpdateAsync(organizationId, Guid.Empty, _webhookRequestModel));
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_IntegrationDoesNotExist_ThrowsNotFound(
        SutProvider<OrganizationIntegrationController> sutProvider,
        Guid organizationId)
    {
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .ReturnsNull();

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.UpdateAsync(organizationId, Guid.Empty, _webhookRequestModel));
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_UserIsNotOrganizationAdmin_ThrowsNotFound(
        SutProvider<OrganizationIntegrationController> sutProvider,
        Guid organizationId)
    {
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.UpdateAsync(organizationId, Guid.Empty, _webhookRequestModel));
    }
}
