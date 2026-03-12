using Bit.Api.Dirt.Controllers;
using Bit.Api.Dirt.Models.Request;
using Bit.Api.Dirt.Models.Response;
using Bit.Core.Context;
using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.EventIntegrations.OrganizationIntegrationConfigurations.Interfaces;
using Bit.Core.Exceptions;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Dirt.Controllers;

[ControllerCustomize(typeof(OrganizationIntegrationConfigurationController))]
[SutProviderCustomize]
public class OrganizationIntegrationsConfigurationControllerTests
{
    [Theory, BitAutoData]
    public async Task DeleteAsync_AllParamsProvided_Succeeds(
        SutProvider<OrganizationIntegrationConfigurationController> sutProvider,
        Guid organizationId,
        Guid integrationId,
        Guid configurationId)
    {
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(true);

        await sutProvider.Sut.DeleteAsync(organizationId, integrationId, configurationId);

        await sutProvider.GetDependency<IDeleteOrganizationIntegrationConfigurationCommand>().Received(1)
            .DeleteAsync(organizationId, integrationId, configurationId);
    }

    [Theory, BitAutoData]
    [Obsolete("Obsolete")]
    public async Task PostDeleteAsync_AllParamsProvided_Succeeds(
        SutProvider<OrganizationIntegrationConfigurationController> sutProvider,
        Guid organizationId,
        Guid integrationId,
        Guid configurationId)
    {
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(true);

        await sutProvider.Sut.PostDeleteAsync(organizationId, integrationId, configurationId);

        await sutProvider.GetDependency<IDeleteOrganizationIntegrationConfigurationCommand>().Received(1)
            .DeleteAsync(organizationId, integrationId, configurationId);
    }

    [Theory, BitAutoData]
    public async Task DeleteAsync_UserIsNotOrganizationAdmin_ThrowsNotFound(
        SutProvider<OrganizationIntegrationConfigurationController> sutProvider,
        Guid organizationId,
        Guid integrationId,
        Guid configurationId)
    {
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(async () =>
            await sutProvider.Sut.DeleteAsync(organizationId, integrationId, configurationId));
    }

    [Theory, BitAutoData]
    public async Task GetAsync_ConfigurationsExist_Succeeds(
        SutProvider<OrganizationIntegrationConfigurationController> sutProvider,
        Guid organizationId,
        Guid integrationId,
        List<OrganizationIntegrationConfiguration> configurations)
    {
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(true);
        sutProvider.GetDependency<IGetOrganizationIntegrationConfigurationsQuery>()
            .GetManyByIntegrationAsync(organizationId, integrationId)
            .Returns(configurations);

        var result = await sutProvider.Sut.GetAsync(organizationId, integrationId);

        Assert.NotNull(result);
        Assert.Equal(configurations.Count, result.Count);
        Assert.All(result, r => Assert.IsType<OrganizationIntegrationConfigurationResponseModel>(r));
        await sutProvider.GetDependency<IGetOrganizationIntegrationConfigurationsQuery>().Received(1)
            .GetManyByIntegrationAsync(organizationId, integrationId);
    }

    [Theory, BitAutoData]
    public async Task GetAsync_NoConfigurationsExist_ReturnsEmptyList(
        SutProvider<OrganizationIntegrationConfigurationController> sutProvider,
        Guid organizationId,
        Guid integrationId)
    {
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(true);
        sutProvider.GetDependency<IGetOrganizationIntegrationConfigurationsQuery>()
            .GetManyByIntegrationAsync(organizationId, integrationId)
            .Returns([]);

        var result = await sutProvider.Sut.GetAsync(organizationId, integrationId);

        Assert.NotNull(result);
        Assert.Empty(result);
        await sutProvider.GetDependency<IGetOrganizationIntegrationConfigurationsQuery>().Received(1)
            .GetManyByIntegrationAsync(organizationId, integrationId);
    }

    [Theory, BitAutoData]
    public async Task GetAsync_UserIsNotOrganizationAdmin_ThrowsNotFound(
        SutProvider<OrganizationIntegrationConfigurationController> sutProvider,
        Guid organizationId,
        Guid integrationId)
    {
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(async () =>
            await sutProvider.Sut.GetAsync(organizationId, integrationId));
    }

    [Theory, BitAutoData]
    public async Task PostAsync_AllParamsProvided_Succeeds(
        SutProvider<OrganizationIntegrationConfigurationController> sutProvider,
        Guid organizationId,
        Guid integrationId,
        OrganizationIntegrationConfiguration configuration,
        OrganizationIntegrationConfigurationRequestModel model)
    {
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(true);
        sutProvider.GetDependency<ICreateOrganizationIntegrationConfigurationCommand>()
            .CreateAsync(organizationId, integrationId, Arg.Any<OrganizationIntegrationConfiguration>())
            .Returns(configuration);

        var createResponse = await sutProvider.Sut.CreateAsync(organizationId, integrationId, model);

        await sutProvider.GetDependency<ICreateOrganizationIntegrationConfigurationCommand>().Received(1)
            .CreateAsync(organizationId, integrationId, Arg.Any<OrganizationIntegrationConfiguration>());
        Assert.IsType<OrganizationIntegrationConfigurationResponseModel>(createResponse);
    }

    [Theory, BitAutoData]
    public async Task PostAsync_UserIsNotOrganizationAdmin_ThrowsNotFound(
        SutProvider<OrganizationIntegrationConfigurationController> sutProvider,
        Guid organizationId,
        Guid integrationId)
    {
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(async () =>
            await sutProvider.Sut.CreateAsync(organizationId, integrationId, new OrganizationIntegrationConfigurationRequestModel()));
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_AllParamsProvided_Succeeds(
        SutProvider<OrganizationIntegrationConfigurationController> sutProvider,
        Guid organizationId,
        Guid integrationId,
        Guid configurationId,
        OrganizationIntegrationConfiguration configuration,
        OrganizationIntegrationConfigurationRequestModel model)
    {
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(true);
        sutProvider.GetDependency<IUpdateOrganizationIntegrationConfigurationCommand>()
            .UpdateAsync(organizationId, integrationId, configurationId, Arg.Any<OrganizationIntegrationConfiguration>())
            .Returns(configuration);

        var updateResponse = await sutProvider.Sut.UpdateAsync(organizationId, integrationId, configurationId, model);

        await sutProvider.GetDependency<IUpdateOrganizationIntegrationConfigurationCommand>().Received(1)
            .UpdateAsync(organizationId, integrationId, configurationId, Arg.Any<OrganizationIntegrationConfiguration>());
        Assert.IsType<OrganizationIntegrationConfigurationResponseModel>(updateResponse);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_UserIsNotOrganizationAdmin_ThrowsNotFound(
        SutProvider<OrganizationIntegrationConfigurationController> sutProvider,
        Guid organizationId,
        Guid integrationId,
        Guid configurationId)
    {
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(async () =>
            await sutProvider.Sut.UpdateAsync(organizationId, integrationId, configurationId, new OrganizationIntegrationConfigurationRequestModel()));
    }
}
