using Bit.Api.AdminConsole.Controllers;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.AdminConsole.Models.Response.Organizations;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.EventIntegrations.OrganizationIntegrations.Interfaces;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Controllers;

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
        sutProvider.GetDependency<IGetOrganizationIntegrationsQuery>()
            .GetManyByOrganizationAsync(organizationId)
            .Returns(integrations);

        var result = await sutProvider.Sut.GetAsync(organizationId);

        await sutProvider.GetDependency<IGetOrganizationIntegrationsQuery>().Received(1)
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
        sutProvider.GetDependency<IGetOrganizationIntegrationsQuery>()
            .GetManyByOrganizationAsync(organizationId)
            .Returns([]);

        var result = await sutProvider.Sut.GetAsync(organizationId);

        Assert.Empty(result);
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
            .CreateAsync(Arg.Any<OrganizationIntegration>())
            .Returns(integration);

        var response = await sutProvider.Sut.CreateAsync(organizationId, _webhookRequestModel);

        await sutProvider.GetDependency<ICreateOrganizationIntegrationCommand>().Received(1)
            .CreateAsync(Arg.Is<OrganizationIntegration>(i =>
                i.OrganizationId == organizationId &&
                i.Type == IntegrationType.Webhook));
        Assert.IsType<OrganizationIntegrationResponseModel>(response);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_UserIsNotOrganizationAdmin_ThrowsNotFound(
        SutProvider<OrganizationIntegrationController> sutProvider,
        Guid organizationId)
    {
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(async () =>
            await sutProvider.Sut.CreateAsync(organizationId, _webhookRequestModel));
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
    public async Task DeleteAsync_UserIsNotOrganizationAdmin_ThrowsNotFound(
        SutProvider<OrganizationIntegrationController> sutProvider,
        Guid organizationId,
        Guid integrationId)
    {
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(async () =>
            await sutProvider.Sut.DeleteAsync(organizationId, integrationId));
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
        Assert.IsType<OrganizationIntegrationResponseModel>(response);
        Assert.Equal(IntegrationType.Webhook, response.Type);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_UserIsNotOrganizationAdmin_ThrowsNotFound(
        SutProvider<OrganizationIntegrationController> sutProvider,
        Guid organizationId,
        Guid integrationId)
    {
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(async () =>
            await sutProvider.Sut.UpdateAsync(organizationId, integrationId, _webhookRequestModel));
    }
}
