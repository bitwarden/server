﻿using Bit.Api.AdminConsole.Controllers;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.AdminConsole.Models.Response.Organizations;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Context;
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
    public async Task PostAsync_AllParamsProvided_Succeeds(
        SutProvider<OrganizationIntegrationConfigurationController> sutProvider,
        Guid organizationId,
        OrganizationIntegration organizationIntegration,
        OrganizationIntegrationConfiguration organizationIntegrationConfiguration,
        OrganizationIntegrationConfigurationRequestModel model)
    {
        organizationIntegration.OrganizationId = organizationId;
        model.OrganizationIntegrationId = organizationIntegration.Id;

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
        var requestAction = await sutProvider.Sut.PostAsync(organizationId, organizationIntegration.Id, model);

        await sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>().Received(1)
            .CreateAsync(Arg.Any<OrganizationIntegrationConfiguration>());
        Assert.IsType<OrganizationIntegrationConfigurationResponseModel>(requestAction);
        Assert.Equal(expected.Id, requestAction.Id);
        Assert.Equal(expected.Configuration, requestAction.Configuration);
        Assert.Equal(expected.EventType, requestAction.EventType);
        Assert.Equal(expected.Template, requestAction.Template);
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

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.PostAsync(
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

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.PostAsync(
            organizationId,
            organizationIntegration.Id,
            new OrganizationIntegrationConfigurationRequestModel()));
    }

    [Theory, BitAutoData]
    public async Task PostAsync_IntegrationDoesNotMatchIdProvided_ThrowsNotFound(
        SutProvider<OrganizationIntegrationConfigurationController> sutProvider,
        Guid organizationId,
        OrganizationIntegration organizationIntegration,
        OrganizationIntegrationConfiguration organizationIntegrationConfiguration,
        OrganizationIntegrationConfigurationRequestModel model)
    {
        organizationIntegration.OrganizationId = organizationId;
        model.OrganizationIntegrationId = organizationIntegration.Id;

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

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.PostAsync(
            organizationId,
            Guid.NewGuid(),
            new OrganizationIntegrationConfigurationRequestModel()));
    }

    [Theory, BitAutoData]
    public async Task PostAsync_UserIsNotOrganizationAdmin_ThrowsNotFound(SutProvider<OrganizationIntegrationConfigurationController> sutProvider, Guid organizationId)
    {
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.PostAsync(organizationId, Guid.Empty, new OrganizationIntegrationConfigurationRequestModel()));
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_AllParamsProvided_Succeeds(
        SutProvider<OrganizationIntegrationConfigurationController> sutProvider,
        Guid organizationId,
        OrganizationIntegration organizationIntegration,
        OrganizationIntegrationConfiguration organizationIntegrationConfiguration,
        OrganizationIntegrationConfigurationRequestModel model)
    {
        organizationIntegration.OrganizationId = organizationId;
        model.OrganizationIntegrationId = organizationIntegration.Id;
        organizationIntegrationConfiguration.OrganizationIntegrationId = organizationIntegration.Id;

        var modelId = model.Id ?? Guid.NewGuid();
        model.Id = modelId;
        var expected = new OrganizationIntegrationConfigurationResponseModel(model.ToOrganizationIntegrationConfiguration());

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
        var requestAction = await sutProvider.Sut.UpdateAsync(organizationId, organizationIntegration.Id, modelId, model);

        await sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>().Received(1)
            .ReplaceAsync(Arg.Any<OrganizationIntegrationConfiguration>());
        Assert.IsType<OrganizationIntegrationConfigurationResponseModel>(requestAction);
        Assert.Equal(expected.Id, requestAction.Id);
        Assert.Equal(expected.Configuration, requestAction.Configuration);
        Assert.Equal(expected.EventType, requestAction.EventType);
        Assert.Equal(expected.Template, requestAction.Template);
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
    public async Task UpdateAsync_IntegrationDoesNotMatchIdProvided_ThrowsNotFound(
        SutProvider<OrganizationIntegrationConfigurationController> sutProvider,
        Guid organizationId,
        OrganizationIntegration organizationIntegration,
        OrganizationIntegrationConfiguration organizationIntegrationConfiguration,
        OrganizationIntegrationConfigurationRequestModel model)
    {
        organizationIntegration.OrganizationId = organizationId;
        model.OrganizationIntegrationId = organizationIntegration.Id;

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

        await Assert.ThrowsAsync<BadRequestException>(async () => await sutProvider.Sut.UpdateAsync(
            organizationId,
            Guid.NewGuid(),
            Guid.Empty,
            new OrganizationIntegrationConfigurationRequestModel()));
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
