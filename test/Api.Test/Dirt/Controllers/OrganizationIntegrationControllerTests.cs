using Bit.Api.Dirt.Controllers;
using Bit.Api.Dirt.Models.Request;
using Bit.Api.Dirt.Models.Response;
using Bit.Core.Context;
using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Enums;
using Bit.Core.Dirt.EventIntegrations.OrganizationIntegrations.Interfaces;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Dirt.Controllers;

[ControllerCustomize(typeof(OrganizationIntegrationController))]
[SutProviderCustomize]
public class OrganizationIntegrationControllerTests
{
    private readonly OrganizationIntegrationRequestModel _webhookRequestModel = new()
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

        var result = await sutProvider.Sut.GetAsync(organizationId);
        Assert.IsType<NotFoundResult>(result.Result);
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
        sutProvider.GetDependency<IGetOrganizationIntegrationsQuery>()
            .GetManyByOrganizationAsync(organizationId)
            .Returns(integrations);

        var result = await sutProvider.Sut.GetAsync(organizationId);

        await sutProvider.GetDependency<IGetOrganizationIntegrationsQuery>().Received(1)
            .GetManyByOrganizationAsync(organizationId);

        Assert.IsType<OkObjectResult>(result.Result);
        var okResult = result.Result as OkObjectResult;
        var returnedIntegrations = okResult.Value as List<OrganizationIntegrationResponseModel>;
        Assert.Equal(integrations.Count, returnedIntegrations.Count);
        Assert.All(returnedIntegrations, r => Assert.IsType<OrganizationIntegrationResponseModel>(r));
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
        sutProvider.GetDependency<IGetOrganizationIntegrationsQuery>()
            .GetManyByOrganizationAsync(organizationId)
            .Returns(new List<OrganizationIntegration>());

        var result = await sutProvider.Sut.GetAsync(organizationId);

        var okResult = result.Result as OkObjectResult;
        var returnedIntegrations = okResult.Value as List<OrganizationIntegrationResponseModel>;
        Assert.Empty(returnedIntegrations);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_AllParamsProvided_Succeeds(
        SutProvider<OrganizationIntegrationController> sutProvider,
        Guid organizationId,
        OrganizationIntegration integration)
    {
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(true);
        sutProvider.GetDependency<ICreateOrganizationIntegrationCommand>()
            .CanCreateAsync(Arg.Any<OrganizationIntegration>())
            .Returns(true);
        sutProvider.GetDependency<ICreateOrganizationIntegrationCommand>()
            .CreateAsync(Arg.Any<OrganizationIntegration>())
            .Returns(integration);

        var response = await sutProvider.Sut.CreateAsync(organizationId, _webhookRequestModel);

        await sutProvider.GetDependency<ICreateOrganizationIntegrationCommand>().Received(1)
            .CreateAsync(Arg.Is<OrganizationIntegration>(i =>
                i.OrganizationId == organizationId &&
                i.Type == IntegrationType.Webhook));
        Assert.IsType<ActionResult<OrganizationIntegrationResponseModel>>(response);
        Assert.IsType<OkObjectResult>(response.Result);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_TheTypeAlreadyExists_ThrowsConflict(
        SutProvider<OrganizationIntegrationController> sutProvider,
        Guid organizationId,
        OrganizationIntegration integration)
    {
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(true);
        sutProvider.GetDependency<ICreateOrganizationIntegrationCommand>()
            .CanCreateAsync(Arg.Any<OrganizationIntegration>())
            .Returns(false);
        sutProvider.GetDependency<ICreateOrganizationIntegrationCommand>()
            .CreateAsync(Arg.Any<OrganizationIntegration>())
            .Returns(integration);

        var response = await sutProvider.Sut.CreateAsync(organizationId, _webhookRequestModel);

        Assert.IsType<ActionResult<OrganizationIntegrationResponseModel>>(response);
        Assert.IsType<ConflictResult>(response.Result);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_UserIsNotOrganizationAdmin_ReturnsNotFound(
        SutProvider<OrganizationIntegrationController> sutProvider,
        Guid organizationId)
    {
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(false);

        var response = await sutProvider.Sut.CreateAsync(organizationId, _webhookRequestModel);

        Assert.IsType<NotFoundResult>(response.Result);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_ExceptionThrown_ReturnsBadRequest(
        SutProvider<OrganizationIntegrationController> sutProvider,
        Guid organizationId)
    {
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(true);
        sutProvider.GetDependency<ICreateOrganizationIntegrationCommand>()
            .CanCreateAsync(Arg.Any<OrganizationIntegration>())
            .Returns(true);
        sutProvider.GetDependency<ICreateOrganizationIntegrationCommand>()
            .CreateAsync(Arg.Any<OrganizationIntegration>())
            .Returns(Task.FromException<OrganizationIntegration>(new Exception()));

        var response = await sutProvider.Sut.CreateAsync(organizationId, _webhookRequestModel);

        Assert.IsType<BadRequestResult>(response.Result);
    }

    [Theory, BitAutoData]
    public async Task DeleteAsync_AllParamsProvided_Succeeds(
        SutProvider<OrganizationIntegrationController> sutProvider,
        Guid organizationId,
        Guid integrationId)
    {
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(true);

        await sutProvider.Sut.DeleteAsync(organizationId, integrationId);

        await sutProvider.GetDependency<IDeleteOrganizationIntegrationCommand>().Received(1)
            .DeleteAsync(organizationId, integrationId);
    }

    [Theory, BitAutoData]
    [Obsolete("Obsolete")]
    public async Task PostDeleteAsync_AllParamsProvided_Succeeds(
        SutProvider<OrganizationIntegrationController> sutProvider,
        Guid organizationId,
        Guid integrationId)
    {
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(true);

        await sutProvider.Sut.PostDeleteAsync(organizationId, integrationId);

        await sutProvider.GetDependency<IDeleteOrganizationIntegrationCommand>().Received(1)
            .DeleteAsync(organizationId, integrationId);
    }

    [Theory, BitAutoData]
    public async Task DeleteAsync_UserIsNotOrganizationAdmin_ReturnsNotFound(
        SutProvider<OrganizationIntegrationController> sutProvider,
        Guid organizationId,
        Guid integrationId)
    {
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(false);

        var response = await sutProvider.Sut.DeleteAsync(organizationId, integrationId);

        Assert.IsType<NotFoundResult>(response);
    }

    [Theory, BitAutoData]
    public async Task DeleteAsync_ExceptionThrown_ReturnsBadRequest(
        SutProvider<OrganizationIntegrationController> sutProvider,
        Guid organizationId,
        Guid integrationId)
    {
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(true);
        sutProvider.GetDependency<IDeleteOrganizationIntegrationCommand>()
            .DeleteAsync(organizationId, integrationId)
            .Returns(Task.FromException(new Exception()));

        var response = await sutProvider.Sut.DeleteAsync(organizationId, integrationId);

        Assert.IsType<BadRequestResult>(response);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_AllParamsProvided_Succeeds(
        SutProvider<OrganizationIntegrationController> sutProvider,
        Guid organizationId,
        Guid integrationId,
        OrganizationIntegration integration)
    {
        integration.OrganizationId = organizationId;
        integration.Id = integrationId;
        integration.Type = IntegrationType.Webhook;

        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(true);
        sutProvider.GetDependency<IUpdateOrganizationIntegrationCommand>()
            .UpdateAsync(organizationId, integrationId, Arg.Any<OrganizationIntegration>())
            .Returns(integration);

        var response = await sutProvider.Sut.UpdateAsync(organizationId, integrationId, _webhookRequestModel);

        await sutProvider.GetDependency<IUpdateOrganizationIntegrationCommand>().Received(1)
            .UpdateAsync(organizationId, integrationId, Arg.Is<OrganizationIntegration>(i =>
                i.OrganizationId == organizationId &&
                i.Type == IntegrationType.Webhook));
        Assert.IsType<OkObjectResult>(response.Result);
        var okResult = response.Result as OkObjectResult;
        Assert.NotNull(okResult);
        var resultValue = okResult!.Value as OrganizationIntegrationResponseModel;
        Assert.NotNull(resultValue);
        Assert.Equal(IntegrationType.Webhook, resultValue!.Type);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_UserIsNotOrganizationAdmin_ReturnsNotFound(
        SutProvider<OrganizationIntegrationController> sutProvider,
        Guid organizationId,
        Guid integrationId)
    {
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(false);

        var response = await sutProvider.Sut.UpdateAsync(organizationId, integrationId, _webhookRequestModel);

        Assert.IsType<NotFoundResult>(response.Result);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_ExceptionThrown_ReturnsBadRequest(
        SutProvider<OrganizationIntegrationController> sutProvider,
        Guid organizationId,
        Guid integrationId)
    {
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(true);
        sutProvider.GetDependency<IUpdateOrganizationIntegrationCommand>()
            .UpdateAsync(organizationId, integrationId, Arg.Any<OrganizationIntegration>())
            .Returns(Task.FromException<OrganizationIntegration>(new Exception()));

        var response = await sutProvider.Sut.UpdateAsync(organizationId, integrationId, _webhookRequestModel);

        Assert.IsType<BadRequestResult>(response.Result);
    }
}
