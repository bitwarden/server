#nullable enable

using Bit.Api.AdminConsole.Controllers;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Data.EventIntegrations;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Controllers;

[ControllerCustomize(typeof(SlackIntegrationController))]
[SutProviderCustomize]
public class SlackIntegrationControllerTests
{
    private const string _slackToken = "xoxb-test-token";
    private const string _validSlackCode = "A_test_code";

    [Theory, BitAutoData]
    public async Task CreateAsync_AllParamsProvided_Succeeds(
        SutProvider<SlackIntegrationController> sutProvider,
        OrganizationIntegration integration)
    {
        integration.Type = IntegrationType.Slack;
        integration.Configuration = null;
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.Sut.Url
            .RouteUrl(Arg.Is<UrlRouteContext>(c => c.RouteName == "SlackIntegration_Create"))
            .Returns("https://localhost");
        sutProvider.GetDependency<ISlackService>()
            .ObtainTokenViaOAuth(_validSlackCode, Arg.Any<string>())
            .Returns(_slackToken);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integration.Id)
            .Returns(integration);

        var state = IntegrationOAuthState.FromIntegration(integration, sutProvider.GetDependency<TimeProvider>());
        var requestAction = await sutProvider.Sut.CreateAsync(_validSlackCode, state.ToString());

        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().Received(1)
            .UpsertAsync(Arg.Any<OrganizationIntegration>());
        Assert.IsType<CreatedResult>(requestAction);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_CodeIsEmpty_ThrowsBadRequest(
        SutProvider<SlackIntegrationController> sutProvider,
        OrganizationIntegration integration)
    {
        integration.Type = IntegrationType.Slack;
        integration.Configuration = null;
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.Sut.Url
            .RouteUrl(Arg.Is<UrlRouteContext>(c => c.RouteName == "SlackIntegration_Create"))
            .Returns("https://localhost");
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integration.Id)
            .Returns(integration);
        var state = IntegrationOAuthState.FromIntegration(integration, sutProvider.GetDependency<TimeProvider>());

        await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sutProvider.Sut.CreateAsync(string.Empty, state.ToString()));
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_SlackServiceReturnsEmpty_ThrowsBadRequest(
        SutProvider<SlackIntegrationController> sutProvider,
        OrganizationIntegration integration)
    {
        integration.Type = IntegrationType.Slack;
        integration.Configuration = null;
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.Sut.Url
            .RouteUrl(Arg.Is<UrlRouteContext>(c => c.RouteName == "SlackIntegration_Create"))
            .Returns("https://localhost");
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integration.Id)
            .Returns(integration);
        sutProvider.GetDependency<ISlackService>()
            .ObtainTokenViaOAuth(_validSlackCode, Arg.Any<string>())
            .Returns(string.Empty);
        var state = IntegrationOAuthState.FromIntegration(integration, sutProvider.GetDependency<TimeProvider>());

        await Assert.ThrowsAsync<BadRequestException>(async () => await sutProvider.Sut.CreateAsync(_validSlackCode, state.ToString()));
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_StateEmpty_ThrowsNotFound(
        SutProvider<SlackIntegrationController> sutProvider)
    {
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.Sut.Url
            .RouteUrl(Arg.Is<UrlRouteContext>(c => c.RouteName == "SlackIntegration_Create"))
            .Returns("https://localhost");
        sutProvider.GetDependency<ISlackService>()
            .ObtainTokenViaOAuth(_validSlackCode, Arg.Any<string>())
            .Returns(_slackToken);

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.CreateAsync(_validSlackCode, string.Empty));
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_StateExpired_ThrowsNotFound(
        SutProvider<SlackIntegrationController> sutProvider,
        OrganizationIntegration integration)
    {
        var timeProvider = new FakeTimeProvider(new DateTime(2024, 4, 3, 2, 1, 0, DateTimeKind.Utc));
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.Sut.Url
            .RouteUrl(Arg.Is<UrlRouteContext>(c => c.RouteName == "SlackIntegration_Create"))
            .Returns("https://localhost");
        sutProvider.GetDependency<ISlackService>()
            .ObtainTokenViaOAuth(_validSlackCode, Arg.Any<string>())
            .Returns(_slackToken);
        var state = IntegrationOAuthState.FromIntegration(integration, timeProvider);
        timeProvider.Advance(TimeSpan.FromMinutes(30));

        sutProvider.SetDependency<TimeProvider>(timeProvider);
        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.CreateAsync(_validSlackCode, state.ToString()));
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_StateHasNonexistentIntegration_ThrowsNotFound(
        SutProvider<SlackIntegrationController> sutProvider,
        OrganizationIntegration integration)
    {
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.Sut.Url
            .RouteUrl(Arg.Is<UrlRouteContext>(c => c.RouteName == "SlackIntegration_Create"))
            .Returns("https://localhost");
        sutProvider.GetDependency<ISlackService>()
            .ObtainTokenViaOAuth(_validSlackCode, Arg.Any<string>())
            .Returns(_slackToken);

        var state = IntegrationOAuthState.FromIntegration(integration, sutProvider.GetDependency<TimeProvider>());

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.CreateAsync(_validSlackCode, state.ToString()));
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_StateHasWrongOrganizationHash_ThrowsNotFound(
        SutProvider<SlackIntegrationController> sutProvider,
        OrganizationIntegration integration,
        OrganizationIntegration wrongOrgIntegration)
    {
        wrongOrgIntegration.Id = integration.Id;

        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.Sut.Url
            .RouteUrl(Arg.Is<UrlRouteContext>(c => c.RouteName == "SlackIntegration_Create"))
            .Returns("https://localhost");
        sutProvider.GetDependency<ISlackService>()
            .ObtainTokenViaOAuth(_validSlackCode, Arg.Any<string>())
            .Returns(_slackToken);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integration.Id)
            .Returns(wrongOrgIntegration);

        var state = IntegrationOAuthState.FromIntegration(integration, sutProvider.GetDependency<TimeProvider>());

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.CreateAsync(_validSlackCode, state.ToString()));
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_StateHasNonEmptyIntegration_ThrowsNotFound(
        SutProvider<SlackIntegrationController> sutProvider,
        OrganizationIntegration integration)
    {
        integration.Type = IntegrationType.Slack;
        integration.Configuration = "{}";
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.Sut.Url
            .RouteUrl(Arg.Is<UrlRouteContext>(c => c.RouteName == "SlackIntegration_Create"))
            .Returns("https://localhost");
        sutProvider.GetDependency<ISlackService>()
            .ObtainTokenViaOAuth(_validSlackCode, Arg.Any<string>())
            .Returns(_slackToken);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integration.Id)
            .Returns(integration);

        var state = IntegrationOAuthState.FromIntegration(integration, sutProvider.GetDependency<TimeProvider>());
        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.CreateAsync(_validSlackCode, state.ToString()));
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_StateHasNonSlackIntegration_ThrowsNotFound(
        SutProvider<SlackIntegrationController> sutProvider,
        OrganizationIntegration integration)
    {
        integration.Type = IntegrationType.Hec;
        integration.Configuration = null;
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.Sut.Url
            .RouteUrl(Arg.Is<UrlRouteContext>(c => c.RouteName == "SlackIntegration_Create"))
            .Returns("https://localhost");
        sutProvider.GetDependency<ISlackService>()
            .ObtainTokenViaOAuth(_validSlackCode, Arg.Any<string>())
            .Returns(_slackToken);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integration.Id)
            .Returns(integration);

        var state = IntegrationOAuthState.FromIntegration(integration, sutProvider.GetDependency<TimeProvider>());
        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.CreateAsync(_validSlackCode, state.ToString()));
    }

    [Theory, BitAutoData]
    public async Task RedirectAsync_Success(
        SutProvider<SlackIntegrationController> sutProvider,
        OrganizationIntegration integration)
    {
        integration.Configuration = null;
        var expectedUrl = "https://localhost/";

        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.Sut.Url
            .RouteUrl(Arg.Is<UrlRouteContext>(c => c.RouteName == "SlackIntegration_Create"))
            .Returns(expectedUrl);
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(integration.OrganizationId)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetManyByOrganizationAsync(integration.OrganizationId)
            .Returns([]);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .CreateAsync(Arg.Any<OrganizationIntegration>())
            .Returns(integration);
        sutProvider.GetDependency<ISlackService>().GetRedirectUrl(Arg.Any<string>(), Arg.Any<string>()).Returns(expectedUrl);

        var expectedState = IntegrationOAuthState.FromIntegration(integration, sutProvider.GetDependency<TimeProvider>());

        var requestAction = await sutProvider.Sut.RedirectAsync(integration.OrganizationId);

        Assert.IsType<RedirectResult>(requestAction);
        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().Received(1)
            .CreateAsync(Arg.Any<OrganizationIntegration>());
        sutProvider.GetDependency<ISlackService>().Received(1).GetRedirectUrl(Arg.Any<string>(), expectedState.ToString());
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
            .RouteUrl(Arg.Is<UrlRouteContext>(c => c.RouteName == "SlackIntegration_Create"))
            .Returns(expectedUrl);
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetManyByOrganizationAsync(organizationId)
            .Returns([integration]);
        sutProvider.GetDependency<ISlackService>().GetRedirectUrl(Arg.Any<string>(), Arg.Any<string>()).Returns(expectedUrl);

        var requestAction = await sutProvider.Sut.RedirectAsync(organizationId);

        var expectedState = IntegrationOAuthState.FromIntegration(integration, sutProvider.GetDependency<TimeProvider>());

        Assert.IsType<RedirectResult>(requestAction);
        sutProvider.GetDependency<ISlackService>().Received(1).GetRedirectUrl(Arg.Any<string>(), expectedState.ToString());
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
            .RouteUrl(Arg.Is<UrlRouteContext>(c => c.RouteName == "SlackIntegration_Create"))
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
            .RouteUrl(Arg.Is<UrlRouteContext>(c => c.RouteName == "SlackIntegration_Create"))
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
