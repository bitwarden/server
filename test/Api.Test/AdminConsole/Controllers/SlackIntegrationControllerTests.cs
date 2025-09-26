#nullable enable

using Bit.Api.AdminConsole.Controllers;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Controllers;

[ControllerCustomize(typeof(SlackIntegrationController))]
[SutProviderCustomize]
public class SlackIntegrationControllerTests
{
    [Theory, BitAutoData]
    public async Task CreateAsync_AllParamsProvided_Succeeds(
        SutProvider<SlackIntegrationController> sutProvider,
        OrganizationIntegration integration)
    {
        var token = "xoxb-test-token";
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.Sut.Url
            .RouteUrl(Arg.Is<UrlRouteContext>(c => c.RouteName == nameof(SlackIntegrationController.CreateAsync)))
            .Returns("https://localhost");
        sutProvider.GetDependency<ISlackService>()
            .ObtainTokenViaOAuth("A_test_code", Arg.Any<string>())
            .Returns(token);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integration.Id)
            .Returns(integration);
        var requestAction = await sutProvider.Sut.CreateAsync("A_test_code", integration.Id.ToString());

        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().Received(1)
            .UpsertAsync(Arg.Any<OrganizationIntegration>());
        Assert.IsType<CreatedResult>(requestAction);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_CodeIsEmpty_ThrowsBadRequest(
        SutProvider<SlackIntegrationController> sutProvider,
        OrganizationIntegration integration)
    {
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.Sut.Url
            .RouteUrl(Arg.Is<UrlRouteContext>(c => c.RouteName == nameof(SlackIntegrationController.CreateAsync)))
            .Returns("https://localhost");
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integration.Id)
            .Returns(integration);

        await Assert.ThrowsAsync<BadRequestException>(async () => await sutProvider.Sut.CreateAsync(string.Empty, integration.Id.ToString()));
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_SlackServiceReturnsEmpty_ThrowsBadRequest(
        SutProvider<SlackIntegrationController> sutProvider,
        OrganizationIntegration integration)
    {
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.Sut.Url
            .RouteUrl(Arg.Is<UrlRouteContext>(c => c.RouteName == nameof(SlackIntegrationController.CreateAsync)))
            .Returns("https://localhost");
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integration.Id)
            .Returns(integration);
        sutProvider.GetDependency<ISlackService>()
            .ObtainTokenViaOAuth("A_test_code", Arg.Any<string>())
            .Returns(string.Empty);

        await Assert.ThrowsAsync<BadRequestException>(async () => await sutProvider.Sut.CreateAsync("A_test_code", integration.Id.ToString()));
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_NoIntegrationFoundByStateId_ThrowsNotFound(
        SutProvider<SlackIntegrationController> sutProvider)
    {
        var token = "xoxb-test-token";
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.Sut.Url
            .RouteUrl(Arg.Is<UrlRouteContext>(c => c.RouteName == nameof(SlackIntegrationController.CreateAsync)))
            .Returns("https://localhost");
        sutProvider.GetDependency<ISlackService>()
            .ObtainTokenViaOAuth("A_test_code", Arg.Any<string>())
            .Returns(token);

        await Assert.ThrowsAsync<BadRequestException>(async () => await sutProvider.Sut.CreateAsync("A_test_code", Guid.NewGuid().ToString()));
    }

    [Theory, BitAutoData]
    public async Task RedirectAsync_Success(
        SutProvider<SlackIntegrationController> sutProvider,
        Guid organizationId,
        OrganizationIntegration integration)
    {
        integration.OrganizationId = organizationId;
        integration.Configuration = null;
        var expectedUrl = "https://localhost/";

        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.Sut.Url
            .RouteUrl(Arg.Is<UrlRouteContext>(c => c.RouteName == nameof(SlackIntegrationController.CreateAsync)))
            .Returns(expectedUrl);
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetManyByOrganizationAsync(organizationId)
            .Returns([]);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .CreateAsync(Arg.Any<OrganizationIntegration>())
            .Returns(integration);
        sutProvider.GetDependency<ISlackService>().GetRedirectUrl(Arg.Any<string>(), Arg.Any<string>()).Returns(expectedUrl);

        var requestAction = await sutProvider.Sut.RedirectAsync(organizationId);

        Assert.IsType<RedirectResult>(requestAction);
        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().Received(1)
            .CreateAsync(Arg.Any<OrganizationIntegration>());
        sutProvider.GetDependency<ISlackService>().Received(1).GetRedirectUrl(Arg.Any<string>(), integration.Id.ToString());
    }

    [Theory, BitAutoData]
    public async Task RedirectAsync_IntegrationAlreadyExistsWithNullConfig_Success(
        SutProvider<SlackIntegrationController> sutProvider,
        Guid organizationId,
        OrganizationIntegration integration)
    {
        integration.OrganizationId = organizationId;
        integration.Configuration = null;
        integration.Type = IntegrationType.Slack;
        var expectedUrl = "https://localhost/";

        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.Sut.Url
            .RouteUrl(Arg.Is<UrlRouteContext>(c => c.RouteName == nameof(SlackIntegrationController.CreateAsync)))
            .Returns(expectedUrl);
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetManyByOrganizationAsync(organizationId)
            .Returns([integration]);
        sutProvider.GetDependency<ISlackService>().GetRedirectUrl(Arg.Any<string>(), Arg.Any<string>()).Returns(expectedUrl);

        var requestAction = await sutProvider.Sut.RedirectAsync(organizationId);

        Assert.IsType<RedirectResult>(requestAction);
        sutProvider.GetDependency<ISlackService>().Received(1).GetRedirectUrl(Arg.Any<string>(), integration.Id.ToString());
    }

    [Theory, BitAutoData]
    public async Task RedirectAsync_IntegrationAlreadyExistsWithConfig_ThrowsBadRequest(
        SutProvider<SlackIntegrationController> sutProvider,
        Guid organizationId,
        OrganizationIntegration integration)
    {
        integration.OrganizationId = organizationId;
        integration.Configuration = "{}";
        integration.Type = IntegrationType.Slack;
        var expectedUrl = "https://localhost/";

        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.Sut.Url
            .RouteUrl(Arg.Is<UrlRouteContext>(c => c.RouteName == nameof(SlackIntegrationController.CreateAsync)))
            .Returns(expectedUrl);
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetManyByOrganizationAsync(organizationId)
            .Returns([integration]);
        sutProvider.GetDependency<ISlackService>().GetRedirectUrl(Arg.Any<string>(), Arg.Any<string>()).Returns(expectedUrl);

        await Assert.ThrowsAsync<BadRequestException>(async () => await sutProvider.Sut.RedirectAsync(organizationId));
    }

    [Theory, BitAutoData]
    public async Task RedirectAsync_SlackServiceReturnsEmpty_ThrowsNotFound(
        SutProvider<SlackIntegrationController> sutProvider,
        Guid organizationId,
        OrganizationIntegration integration)
    {
        integration.OrganizationId = organizationId;
        integration.Configuration = null;
        var expectedUrl = "https://localhost/";

        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.Sut.Url
            .RouteUrl(Arg.Is<UrlRouteContext>(c => c.RouteName == nameof(SlackIntegrationController.CreateAsync)))
            .Returns(expectedUrl);
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetManyByOrganizationAsync(organizationId)
            .Returns([]);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .CreateAsync(Arg.Any<OrganizationIntegration>())
            .Returns(integration);
        sutProvider.GetDependency<ISlackService>().GetRedirectUrl(Arg.Any<string>(), Arg.Any<string>()).Returns(string.Empty);

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.RedirectAsync(organizationId));
    }

    [Theory, BitAutoData]
    public async Task RedirectAsync_UserIsNotOrganizationAdmin_ThrowsNotFound(SutProvider<SlackIntegrationController> sutProvider,
        Guid organizationId)
    {
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.RedirectAsync(organizationId));
    }
}
