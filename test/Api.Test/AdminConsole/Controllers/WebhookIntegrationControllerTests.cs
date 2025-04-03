using Bit.Api.AdminConsole.Controllers;
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

[ControllerCustomize(typeof(WebhookIntegrationController))]
[SutProviderCustomize]
public class WebhookIntegrationControllerTests
{
    [Theory, BitAutoData]
    public async Task CreateAsync_AllParamsProvided_Succeeds(SutProvider<WebhookIntegrationController> sutProvider, Guid organizationId)
    {
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .CreateAsync(Arg.Any<OrganizationIntegration>())
            .Returns(callInfo => callInfo.Arg<OrganizationIntegration>());
        var requestAction = await sutProvider.Sut.CreateAsync(organizationId);

        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().Received(1)
            .CreateAsync(Arg.Any<OrganizationIntegration>());
        Assert.IsType<OkObjectResult>(requestAction);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_UserIsNotOrganizationAdmin_ThrowsNotFound(SutProvider<WebhookIntegrationController> sutProvider, Guid organizationId)
    {
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.CreateAsync(organizationId));
    }

    [Theory, BitAutoData]
    public async Task DeleteAsync_AllParamsProvided_Succeeds(
        SutProvider<WebhookIntegrationController> sutProvider,
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

        await sutProvider.Sut.DeleteAsync(organizationId, organizationIntegration.Id);

        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().Received(1)
            .GetByIdAsync(organizationIntegration.Id);
        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().Received(1)
            .DeleteAsync(organizationIntegration);
    }

    [Theory, BitAutoData]
    public async Task DeleteAsync_IntegrationDoesNotExist_ThrowsNotFound(
        SutProvider<WebhookIntegrationController> sutProvider,
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
        SutProvider<WebhookIntegrationController> sutProvider,
        Guid organizationId)
    {
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.DeleteAsync(organizationId, Guid.Empty));
    }
}
