using System.Text.Json;
using Bit.Api.AdminConsole.Controllers;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.AdminConsole.Models.Response.Organizations;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Data.EventIntegrations;
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

[ControllerCustomize(typeof(OrganizationIntegrationConfigurationController))]
[SutProviderCustomize]
public class OrganizationIntegrationsConfigurationControllerTests
{
    [Theory, BitAutoData]
    public async Task DeleteAsync_AllParamsProvided_Succeeds(
        SutProvider<OrganizationIntegrationConfigurationController> sutProvider,
        Guid organizationId,
        OrganizationIntegration organizationIntegration,
        OrganizationIntegrationConfiguration organizationIntegrationConfiguration)
    {
        organizationIntegration.OrganizationId = organizationId;
        organizationIntegrationConfiguration.OrganizationIntegrationId = organizationIntegration.Id;
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(organizationIntegration);
        sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(organizationIntegrationConfiguration);

        await sutProvider.Sut.DeleteAsync(organizationId, organizationIntegration.Id, organizationIntegrationConfiguration.Id);

        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().Received(1)
            .GetByIdAsync(organizationIntegration.Id);
        await sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>().Received(1)
            .GetByIdAsync(organizationIntegrationConfiguration.Id);
        await sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>().Received(1)
            .DeleteAsync(organizationIntegrationConfiguration);
    }

    [Theory, BitAutoData]
    public async Task DeleteAsync_IntegrationConfigurationDoesNotExist_ThrowsNotFound(
        SutProvider<OrganizationIntegrationConfigurationController> sutProvider,
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
        sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .ReturnsNull();

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.DeleteAsync(organizationId, Guid.Empty, Guid.Empty));
    }

    [Theory, BitAutoData]
    public async Task DeleteAsync_IntegrationDoesNotExist_ThrowsNotFound(
        SutProvider<OrganizationIntegrationConfigurationController> sutProvider,
        Guid organizationId)
    {
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .ReturnsNull();

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.DeleteAsync(organizationId, Guid.Empty, Guid.Empty));
    }

    [Theory, BitAutoData]
    public async Task DeleteAsync_IntegrationDoesNotBelongToOrganization_ThrowsNotFound(
        SutProvider<OrganizationIntegrationConfigurationController> sutProvider,
        Guid organizationId,
        OrganizationIntegration organizationIntegration)
    {
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(organizationIntegration);

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.DeleteAsync(organizationId, organizationIntegration.Id, Guid.Empty));
    }

    [Theory, BitAutoData]
    public async Task DeleteAsync_IntegrationConfigDoesNotBelongToIntegration_ThrowsNotFound(
        SutProvider<OrganizationIntegrationConfigurationController> sutProvider,
        Guid organizationId,
        OrganizationIntegration organizationIntegration,
        OrganizationIntegrationConfiguration organizationIntegrationConfiguration)
    {
        organizationIntegration.OrganizationId = organizationId;
        organizationIntegrationConfiguration.OrganizationIntegrationId = Guid.Empty;
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(organizationIntegration);
        sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(organizationIntegrationConfiguration);

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.DeleteAsync(organizationId, organizationIntegration.Id, Guid.Empty));
    }

    [Theory, BitAutoData]
    public async Task DeleteAsync_UserIsNotOrganizationAdmin_ThrowsNotFound(
        SutProvider<OrganizationIntegrationConfigurationController> sutProvider,
        Guid organizationId)
    {
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.DeleteAsync(organizationId, Guid.Empty, Guid.Empty));
    }

    [Theory, BitAutoData]
    public async Task GetAsync_ConfigurationsExist_Succeeds(
        SutProvider<OrganizationIntegrationConfigurationController> sutProvider,
        Guid organizationId,
        OrganizationIntegration organizationIntegration,
        List<OrganizationIntegrationConfiguration> organizationIntegrationConfigurations)
    {
        organizationIntegration.OrganizationId = organizationId;
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(organizationIntegration);
        sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>()
            .GetManyByIntegrationAsync(Arg.Any<Guid>())
            .Returns(organizationIntegrationConfigurations);

        var result = await sutProvider.Sut.GetAsync(organizationId, organizationIntegration.Id);
        Assert.NotNull(result);
        Assert.Equal(organizationIntegrationConfigurations.Count, result.Count);
        Assert.All(result, r => Assert.IsType<OrganizationIntegrationConfigurationResponseModel>(r));

        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().Received(1)
            .GetByIdAsync(organizationIntegration.Id);
        await sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>().Received(1)
            .GetManyByIntegrationAsync(organizationIntegration.Id);
    }

    [Theory, BitAutoData]
    public async Task GetAsync_NoConfigurationsExist_ReturnsEmptyList(
        SutProvider<OrganizationIntegrationConfigurationController> sutProvider,
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
        sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>()
            .GetManyByIntegrationAsync(Arg.Any<Guid>())
            .Returns([]);

        var result = await sutProvider.Sut.GetAsync(organizationId, organizationIntegration.Id);
        Assert.NotNull(result);
        Assert.Empty(result);

        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().Received(1)
            .GetByIdAsync(organizationIntegration.Id);
        await sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>().Received(1)
            .GetManyByIntegrationAsync(organizationIntegration.Id);
    }

    // [Theory, BitAutoData]
    // public async Task GetAsync_IntegrationConfigurationDoesNotExist_ThrowsNotFound(
    //     SutProvider<OrganizationIntegrationConfigurationController> sutProvider,
    //     Guid organizationId,
    //     OrganizationIntegration organizationIntegration)
    // {
    //     organizationIntegration.OrganizationId = organizationId;
    //     sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
    //     sutProvider.GetDependency<ICurrentContext>()
    //         .OrganizationOwner(organizationId)
    //         .Returns(true);
    //     sutProvider.GetDependency<IOrganizationIntegrationRepository>()
    //         .GetByIdAsync(Arg.Any<Guid>())
    //         .Returns(organizationIntegration);
    //     sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>()
    //         .GetByIdAsync(Arg.Any<Guid>())
    //         .ReturnsNull();
    //
    //     await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.GetAsync(organizationId, Guid.Empty, Guid.Empty));
    // }
    //
    [Theory, BitAutoData]
    public async Task GetAsync_IntegrationDoesNotExist_ThrowsNotFound(
        SutProvider<OrganizationIntegrationConfigurationController> sutProvider,
        Guid organizationId)
    {
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .ReturnsNull();

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.GetAsync(organizationId, Guid.NewGuid()));
    }

    [Theory, BitAutoData]
    public async Task GetAsync_IntegrationDoesNotBelongToOrganization_ThrowsNotFound(
        SutProvider<OrganizationIntegrationConfigurationController> sutProvider,
        Guid organizationId,
        OrganizationIntegration organizationIntegration)
    {
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(organizationIntegration);

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.GetAsync(organizationId, organizationIntegration.Id));
    }

    [Theory, BitAutoData]
    public async Task GetAsync_UserIsNotOrganizationAdmin_ThrowsNotFound(
        SutProvider<OrganizationIntegrationConfigurationController> sutProvider,
        Guid organizationId)
    {
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.GetAsync(organizationId, Guid.NewGuid()));
    }

    [Theory, BitAutoData]
    public async Task PostAsync_AllParamsProvided_Slack_Succeeds(
        SutProvider<OrganizationIntegrationConfigurationController> sutProvider,
        Guid organizationId,
        OrganizationIntegration organizationIntegration,
        OrganizationIntegrationConfiguration organizationIntegrationConfiguration,
        OrganizationIntegrationConfigurationRequestModel model)
    {
        organizationIntegration.OrganizationId = organizationId;
        organizationIntegration.Type = IntegrationType.Slack;
        var slackConfig = new SlackIntegrationConfiguration(ChannelId: "C123456");
        model.Configuration = JsonSerializer.Serialize(slackConfig);
        model.Template = "Template String";
        model.Filters = null;

        var expected = new OrganizationIntegrationConfigurationResponseModel(organizationIntegrationConfiguration);

        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(organizationIntegration);
        sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>()
            .CreateAsync(Arg.Any<OrganizationIntegrationConfiguration>())
            .Returns(organizationIntegrationConfiguration);
        var requestAction = await sutProvider.Sut.CreateAsync(organizationId, organizationIntegration.Id, model);

        await sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>().Received(1)
            .CreateAsync(Arg.Any<OrganizationIntegrationConfiguration>());
        Assert.IsType<OrganizationIntegrationConfigurationResponseModel>(requestAction);
        Assert.Equal(expected.Id, requestAction.Id);
        Assert.Equal(expected.Configuration, requestAction.Configuration);
        Assert.Equal(expected.EventType, requestAction.EventType);
        Assert.Equal(expected.Template, requestAction.Template);
    }

    [Theory, BitAutoData]
    public async Task PostAsync_AllParamsProvided_Webhook_Succeeds(
        SutProvider<OrganizationIntegrationConfigurationController> sutProvider,
        Guid organizationId,
        OrganizationIntegration organizationIntegration,
        OrganizationIntegrationConfiguration organizationIntegrationConfiguration,
        OrganizationIntegrationConfigurationRequestModel model)
    {
        organizationIntegration.OrganizationId = organizationId;
        organizationIntegration.Type = IntegrationType.Webhook;
        var webhookConfig = new WebhookIntegrationConfiguration(Uri: new Uri("https://localhost"), Scheme: "Bearer", Token: "AUTH-TOKEN");
        model.Configuration = JsonSerializer.Serialize(webhookConfig);
        model.Template = "Template String";
        model.Filters = null;

        var expected = new OrganizationIntegrationConfigurationResponseModel(organizationIntegrationConfiguration);

        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(organizationIntegration);
        sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>()
            .CreateAsync(Arg.Any<OrganizationIntegrationConfiguration>())
            .Returns(organizationIntegrationConfiguration);
        var requestAction = await sutProvider.Sut.CreateAsync(organizationId, organizationIntegration.Id, model);

        await sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>().Received(1)
            .CreateAsync(Arg.Any<OrganizationIntegrationConfiguration>());
        Assert.IsType<OrganizationIntegrationConfigurationResponseModel>(requestAction);
        Assert.Equal(expected.Id, requestAction.Id);
        Assert.Equal(expected.Configuration, requestAction.Configuration);
        Assert.Equal(expected.EventType, requestAction.EventType);
        Assert.Equal(expected.Template, requestAction.Template);
    }

    [Theory, BitAutoData]
    public async Task PostAsync_OnlyUrlProvided_Webhook_Succeeds(
        SutProvider<OrganizationIntegrationConfigurationController> sutProvider,
        Guid organizationId,
        OrganizationIntegration organizationIntegration,
        OrganizationIntegrationConfiguration organizationIntegrationConfiguration,
        OrganizationIntegrationConfigurationRequestModel model)
    {
        organizationIntegration.OrganizationId = organizationId;
        organizationIntegration.Type = IntegrationType.Webhook;
        var webhookConfig = new WebhookIntegrationConfiguration(Uri: new Uri("https://localhost"));
        model.Configuration = JsonSerializer.Serialize(webhookConfig);
        model.Template = "Template String";
        model.Filters = null;

        var expected = new OrganizationIntegrationConfigurationResponseModel(organizationIntegrationConfiguration);

        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(organizationIntegration);
        sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>()
            .CreateAsync(Arg.Any<OrganizationIntegrationConfiguration>())
            .Returns(organizationIntegrationConfiguration);
        var requestAction = await sutProvider.Sut.CreateAsync(organizationId, organizationIntegration.Id, model);

        await sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>().Received(1)
            .CreateAsync(Arg.Any<OrganizationIntegrationConfiguration>());
        Assert.IsType<OrganizationIntegrationConfigurationResponseModel>(requestAction);
        Assert.Equal(expected.Id, requestAction.Id);
        Assert.Equal(expected.Configuration, requestAction.Configuration);
        Assert.Equal(expected.EventType, requestAction.EventType);
        Assert.Equal(expected.Template, requestAction.Template);
    }

    [Theory, BitAutoData]
    public async Task PostAsync_IntegrationTypeCloudBillingSync_ThrowsBadRequestException(
        SutProvider<OrganizationIntegrationConfigurationController> sutProvider,
        Guid organizationId,
        OrganizationIntegration organizationIntegration,
        OrganizationIntegrationConfiguration organizationIntegrationConfiguration,
        OrganizationIntegrationConfigurationRequestModel model)
    {
        organizationIntegration.OrganizationId = organizationId;
        organizationIntegration.Type = IntegrationType.CloudBillingSync;

        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(organizationIntegration);
        sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>()
            .CreateAsync(Arg.Any<OrganizationIntegrationConfiguration>())
            .Returns(organizationIntegrationConfiguration);

        await Assert.ThrowsAsync<BadRequestException>(async () => await sutProvider.Sut.CreateAsync(
            organizationId,
            organizationIntegration.Id,
            model));
    }

    [Theory, BitAutoData]
    public async Task PostAsync_IntegrationTypeScim_ThrowsBadRequestException(
        SutProvider<OrganizationIntegrationConfigurationController> sutProvider,
        Guid organizationId,
        OrganizationIntegration organizationIntegration,
        OrganizationIntegrationConfiguration organizationIntegrationConfiguration,
        OrganizationIntegrationConfigurationRequestModel model)
    {
        organizationIntegration.OrganizationId = organizationId;
        organizationIntegration.Type = IntegrationType.Scim;

        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(organizationIntegration);
        sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>()
            .CreateAsync(Arg.Any<OrganizationIntegrationConfiguration>())
            .Returns(organizationIntegrationConfiguration);

        await Assert.ThrowsAsync<BadRequestException>(async () => await sutProvider.Sut.CreateAsync(
            organizationId,
            organizationIntegration.Id,
            model));
    }

    [Theory, BitAutoData]
    public async Task PostAsync_IntegrationDoesNotExist_ThrowsNotFound(
        SutProvider<OrganizationIntegrationConfigurationController> sutProvider,
        Guid organizationId)
    {
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .ReturnsNull();

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.CreateAsync(
            organizationId,
            Guid.Empty,
            new OrganizationIntegrationConfigurationRequestModel()));
    }

    [Theory, BitAutoData]
    public async Task PostAsync_IntegrationDoesNotBelongToOrganization_ThrowsNotFound(
        SutProvider<OrganizationIntegrationConfigurationController> sutProvider,
        Guid organizationId,
        OrganizationIntegration organizationIntegration)
    {
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(organizationIntegration);

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.CreateAsync(
            organizationId,
            organizationIntegration.Id,
            new OrganizationIntegrationConfigurationRequestModel()));
    }

    [Theory, BitAutoData]
    public async Task PostAsync_InvalidConfiguration_ThrowsBadRequestException(
        SutProvider<OrganizationIntegrationConfigurationController> sutProvider,
        Guid organizationId,
        OrganizationIntegration organizationIntegration,
        OrganizationIntegrationConfiguration organizationIntegrationConfiguration,
        OrganizationIntegrationConfigurationRequestModel model)
    {
        organizationIntegration.OrganizationId = organizationId;
        organizationIntegration.Type = IntegrationType.Webhook;
        model.Configuration = null;
        model.Template = "Template String";

        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(organizationIntegration);
        sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>()
            .CreateAsync(Arg.Any<OrganizationIntegrationConfiguration>())
            .Returns(organizationIntegrationConfiguration);

        await Assert.ThrowsAsync<BadRequestException>(async () => await sutProvider.Sut.CreateAsync(
            organizationId,
            organizationIntegration.Id,
            model));
    }

    [Theory, BitAutoData]
    public async Task PostAsync_InvalidTemplate_ThrowsBadRequestException(
        SutProvider<OrganizationIntegrationConfigurationController> sutProvider,
        Guid organizationId,
        OrganizationIntegration organizationIntegration,
        OrganizationIntegrationConfiguration organizationIntegrationConfiguration,
        OrganizationIntegrationConfigurationRequestModel model)
    {
        organizationIntegration.OrganizationId = organizationId;
        organizationIntegration.Type = IntegrationType.Webhook;
        var webhookConfig = new WebhookIntegrationConfiguration(Uri: new Uri("https://localhost"), Scheme: "Bearer", Token: "AUTH-TOKEN");
        model.Configuration = JsonSerializer.Serialize(webhookConfig);
        model.Template = null;

        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(organizationIntegration);
        sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>()
            .CreateAsync(Arg.Any<OrganizationIntegrationConfiguration>())
            .Returns(organizationIntegrationConfiguration);

        await Assert.ThrowsAsync<BadRequestException>(async () => await sutProvider.Sut.CreateAsync(
            organizationId,
            organizationIntegration.Id,
            model));
    }

    [Theory, BitAutoData]
    public async Task PostAsync_UserIsNotOrganizationAdmin_ThrowsNotFound(SutProvider<OrganizationIntegrationConfigurationController> sutProvider, Guid organizationId)
    {
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.CreateAsync(organizationId, Guid.Empty, new OrganizationIntegrationConfigurationRequestModel()));
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_AllParamsProvided_Slack_Succeeds(
        SutProvider<OrganizationIntegrationConfigurationController> sutProvider,
        Guid organizationId,
        OrganizationIntegration organizationIntegration,
        OrganizationIntegrationConfiguration organizationIntegrationConfiguration,
        OrganizationIntegrationConfigurationRequestModel model)
    {
        organizationIntegration.OrganizationId = organizationId;
        organizationIntegrationConfiguration.OrganizationIntegrationId = organizationIntegration.Id;
        organizationIntegration.Type = IntegrationType.Slack;
        var slackConfig = new SlackIntegrationConfiguration(ChannelId: "C123456");
        model.Configuration = JsonSerializer.Serialize(slackConfig);
        model.Template = "Template String";
        model.Filters = null;

        var expected = new OrganizationIntegrationConfigurationResponseModel(model.ToOrganizationIntegrationConfiguration(organizationIntegrationConfiguration));

        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(organizationIntegration);
        sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(organizationIntegrationConfiguration);
        var requestAction = await sutProvider.Sut.UpdateAsync(
            organizationId,
            organizationIntegration.Id,
            organizationIntegrationConfiguration.Id,
            model);

        await sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>().Received(1)
            .ReplaceAsync(Arg.Any<OrganizationIntegrationConfiguration>());
        Assert.IsType<OrganizationIntegrationConfigurationResponseModel>(requestAction);
        Assert.Equal(expected.Id, requestAction.Id);
        Assert.Equal(expected.Configuration, requestAction.Configuration);
        Assert.Equal(expected.EventType, requestAction.EventType);
        Assert.Equal(expected.Template, requestAction.Template);
    }


    [Theory, BitAutoData]
    public async Task UpdateAsync_AllParamsProvided_Webhook_Succeeds(
        SutProvider<OrganizationIntegrationConfigurationController> sutProvider,
        Guid organizationId,
        OrganizationIntegration organizationIntegration,
        OrganizationIntegrationConfiguration organizationIntegrationConfiguration,
        OrganizationIntegrationConfigurationRequestModel model)
    {
        organizationIntegration.OrganizationId = organizationId;
        organizationIntegrationConfiguration.OrganizationIntegrationId = organizationIntegration.Id;
        organizationIntegration.Type = IntegrationType.Webhook;
        var webhookConfig = new WebhookIntegrationConfiguration(Uri: new Uri("https://localhost"), Scheme: "Bearer", Token: "AUTH-TOKEN");
        model.Configuration = JsonSerializer.Serialize(webhookConfig);
        model.Template = "Template String";
        model.Filters = null;

        var expected = new OrganizationIntegrationConfigurationResponseModel(model.ToOrganizationIntegrationConfiguration(organizationIntegrationConfiguration));

        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(organizationIntegration);
        sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(organizationIntegrationConfiguration);
        var requestAction = await sutProvider.Sut.UpdateAsync(
            organizationId,
            organizationIntegration.Id,
            organizationIntegrationConfiguration.Id,
            model);

        await sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>().Received(1)
            .ReplaceAsync(Arg.Any<OrganizationIntegrationConfiguration>());
        Assert.IsType<OrganizationIntegrationConfigurationResponseModel>(requestAction);
        Assert.Equal(expected.Id, requestAction.Id);
        Assert.Equal(expected.Configuration, requestAction.Configuration);
        Assert.Equal(expected.EventType, requestAction.EventType);
        Assert.Equal(expected.Template, requestAction.Template);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_OnlyUrlProvided_Webhook_Succeeds(
        SutProvider<OrganizationIntegrationConfigurationController> sutProvider,
        Guid organizationId,
        OrganizationIntegration organizationIntegration,
        OrganizationIntegrationConfiguration organizationIntegrationConfiguration,
        OrganizationIntegrationConfigurationRequestModel model)
    {
        organizationIntegration.OrganizationId = organizationId;
        organizationIntegrationConfiguration.OrganizationIntegrationId = organizationIntegration.Id;
        organizationIntegration.Type = IntegrationType.Webhook;
        var webhookConfig = new WebhookIntegrationConfiguration(Uri: new Uri("https://localhost"));
        model.Configuration = JsonSerializer.Serialize(webhookConfig);
        model.Template = "Template String";
        model.Filters = null;

        var expected = new OrganizationIntegrationConfigurationResponseModel(model.ToOrganizationIntegrationConfiguration(organizationIntegrationConfiguration));

        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(organizationIntegration);
        sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(organizationIntegrationConfiguration);
        var requestAction = await sutProvider.Sut.UpdateAsync(
            organizationId,
            organizationIntegration.Id,
            organizationIntegrationConfiguration.Id,
            model);

        await sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>().Received(1)
            .ReplaceAsync(Arg.Any<OrganizationIntegrationConfiguration>());
        Assert.IsType<OrganizationIntegrationConfigurationResponseModel>(requestAction);
        Assert.Equal(expected.Id, requestAction.Id);
        Assert.Equal(expected.Configuration, requestAction.Configuration);
        Assert.Equal(expected.EventType, requestAction.EventType);
        Assert.Equal(expected.Template, requestAction.Template);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_IntegrationConfigurationDoesNotExist_ThrowsNotFound(
        SutProvider<OrganizationIntegrationConfigurationController> sutProvider,
        Guid organizationId,
        OrganizationIntegration organizationIntegration,
        OrganizationIntegrationConfigurationRequestModel model)
    {
        organizationIntegration.OrganizationId = organizationId;
        organizationIntegration.Type = IntegrationType.Webhook;
        var webhookConfig = new WebhookIntegrationConfiguration(Uri: new Uri("https://localhost"), Scheme: "Bearer", Token: "AUTH-TOKEN");
        model.Configuration = JsonSerializer.Serialize(webhookConfig);
        model.Template = "Template String";
        model.Filters = null;

        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(organizationIntegration);
        sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .ReturnsNull();

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.UpdateAsync(
            organizationId,
            organizationIntegration.Id,
            Guid.Empty,
            model));
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_IntegrationDoesNotExist_ThrowsNotFound(
        SutProvider<OrganizationIntegrationConfigurationController> sutProvider,
        Guid organizationId)
    {
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .ReturnsNull();

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.UpdateAsync(
            organizationId,
            Guid.Empty,
            Guid.Empty,
            new OrganizationIntegrationConfigurationRequestModel()));
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_IntegrationDoesNotBelongToOrganization_ThrowsNotFound(
        SutProvider<OrganizationIntegrationConfigurationController> sutProvider,
        Guid organizationId,
        OrganizationIntegration organizationIntegration)
    {
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(organizationIntegration);

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.UpdateAsync(
            organizationId,
            organizationIntegration.Id,
            Guid.Empty,
            new OrganizationIntegrationConfigurationRequestModel()));
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_InvalidConfiguration_ThrowsBadRequestException(
        SutProvider<OrganizationIntegrationConfigurationController> sutProvider,
        Guid organizationId,
        OrganizationIntegration organizationIntegration,
        OrganizationIntegrationConfiguration organizationIntegrationConfiguration,
        OrganizationIntegrationConfigurationRequestModel model)
    {
        organizationIntegration.OrganizationId = organizationId;
        organizationIntegrationConfiguration.OrganizationIntegrationId = organizationIntegration.Id;
        organizationIntegration.Type = IntegrationType.Slack;
        model.Configuration = null;
        model.Template = "Template String";

        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(organizationIntegration);
        sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(organizationIntegrationConfiguration);

        await Assert.ThrowsAsync<BadRequestException>(async () => await sutProvider.Sut.UpdateAsync(
            organizationId,
            organizationIntegration.Id,
            organizationIntegrationConfiguration.Id,
            model));
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_InvalidTemplate_ThrowsBadRequestException(
        SutProvider<OrganizationIntegrationConfigurationController> sutProvider,
        Guid organizationId,
        OrganizationIntegration organizationIntegration,
        OrganizationIntegrationConfiguration organizationIntegrationConfiguration,
        OrganizationIntegrationConfigurationRequestModel model)
    {
        organizationIntegration.OrganizationId = organizationId;
        organizationIntegrationConfiguration.OrganizationIntegrationId = organizationIntegration.Id;
        organizationIntegration.Type = IntegrationType.Slack;
        var slackConfig = new SlackIntegrationConfiguration(ChannelId: "C123456");
        model.Configuration = JsonSerializer.Serialize(slackConfig);
        model.Template = null;

        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(organizationIntegration);
        sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(organizationIntegrationConfiguration);

        await Assert.ThrowsAsync<BadRequestException>(async () => await sutProvider.Sut.UpdateAsync(
            organizationId,
            organizationIntegration.Id,
            organizationIntegrationConfiguration.Id,
            model));
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_UserIsNotOrganizationAdmin_ThrowsNotFound(SutProvider<OrganizationIntegrationConfigurationController> sutProvider, Guid organizationId)
    {
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.UpdateAsync(
            organizationId,
            Guid.Empty,
            Guid.Empty,
            new OrganizationIntegrationConfigurationRequestModel()));
    }
}
