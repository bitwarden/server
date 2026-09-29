#nullable enable

using System.Text.Json;
using Bit.Api.Dirt.Controllers;
using Bit.Core.Context;
using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Enums;
using Bit.Core.Dirt.Models.Data.EventIntegrations;
using Bit.Core.Dirt.Models.Data.Teams;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Dirt.Services;
using Bit.Core.Exceptions;
using Bit.Core.Settings;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Dirt.Controllers;

[ControllerCustomize(typeof(TeamsIntegrationController))]
[SutProviderCustomize]
public class TeamsIntegrationControllerTests
{
    private const string _teamsToken = "test-token";
    private const string _validTeamsCode = "A_test_code";

    [Theory, BitAutoData]
    public async Task CreateAsync_AllParamsProvided_Succeeds(
        SutProvider<TeamsIntegrationController> sutProvider,
        OrganizationIntegration integration)
    {
        integration.Type = IntegrationType.Teams;
        integration.Configuration = null;
        SetBaseServiceUriApi(sutProvider);
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.Sut.Url
            .RouteUrl(Arg.Is<UrlRouteContext>(c => c.RouteName == "TeamsIntegration_Create"))
            .Returns("https://localhost");
        sutProvider.GetDependency<ITeamsService>()
            .ObtainTokenViaOAuth(_validTeamsCode, Arg.Any<string>())
            .Returns(_teamsToken);
        sutProvider.GetDependency<ITeamsService>()
            .GetJoinedTeamsAsync(_teamsToken)
            .Returns([
                new TeamInfo() { DisplayName = "Test Team", Id = Guid.NewGuid().ToString(), TenantId = Guid.NewGuid().ToString() }
            ]);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integration.Id)
            .Returns(integration);

        var state = IntegrationOAuthState.FromIntegration(integration, sutProvider.GetDependency<TimeProvider>());
        var requestAction = await sutProvider.Sut.CreateAsync(_validTeamsCode, state.ToString());

        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().Received(1)
            .UpsertAsync(Arg.Any<OrganizationIntegration>());
        Assert.IsType<CreatedResult>(requestAction);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_CallbackUrlIsEmpty_ThrowsBadRequest(
        SutProvider<TeamsIntegrationController> sutProvider,
        OrganizationIntegration integration)
    {
        integration.Type = IntegrationType.Teams;
        integration.Configuration = null;
        SetBaseServiceUriApi(sutProvider);
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.Sut.Url
            .RouteUrl(Arg.Is<UrlRouteContext>(c => c.RouteName == "TeamsIntegration_Create"))
            .Returns((string?)null);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integration.Id)
            .Returns(integration);
        var state = IntegrationOAuthState.FromIntegration(integration, sutProvider.GetDependency<TimeProvider>());

        await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sutProvider.Sut.CreateAsync(_validTeamsCode, state.ToString()));
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_CodeIsEmpty_ThrowsBadRequest(
        SutProvider<TeamsIntegrationController> sutProvider,
        OrganizationIntegration integration)
    {
        integration.Type = IntegrationType.Teams;
        integration.Configuration = null;
        SetBaseServiceUriApi(sutProvider);
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.Sut.Url
            .RouteUrl(Arg.Is<UrlRouteContext>(c => c.RouteName == "TeamsIntegration_Create"))
            .Returns("https://localhost");
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integration.Id)
            .Returns(integration);
        var state = IntegrationOAuthState.FromIntegration(integration, sutProvider.GetDependency<TimeProvider>());

        await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sutProvider.Sut.CreateAsync(string.Empty, state.ToString()));
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_NoTeamsFound_ThrowsBadRequest(
        SutProvider<TeamsIntegrationController> sutProvider,
        OrganizationIntegration integration)
    {
        integration.Type = IntegrationType.Teams;
        integration.Configuration = null;
        SetBaseServiceUriApi(sutProvider);
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.Sut.Url
            .RouteUrl(Arg.Is<UrlRouteContext>(c => c.RouteName == "TeamsIntegration_Create"))
            .Returns("https://localhost");
        sutProvider.GetDependency<ITeamsService>()
            .ObtainTokenViaOAuth(_validTeamsCode, Arg.Any<string>())
            .Returns(_teamsToken);
        sutProvider.GetDependency<ITeamsService>()
            .GetJoinedTeamsAsync(_teamsToken)
            .Returns([]);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integration.Id)
            .Returns(integration);

        var state = IntegrationOAuthState.FromIntegration(integration, sutProvider.GetDependency<TimeProvider>());

        await Assert.ThrowsAsync<BadRequestException>(async () => await sutProvider.Sut.CreateAsync(_validTeamsCode, state.ToString()));
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_TeamsServiceReturnsEmptyToken_ThrowsBadRequest(
        SutProvider<TeamsIntegrationController> sutProvider,
        OrganizationIntegration integration)
    {
        integration.Type = IntegrationType.Teams;
        integration.Configuration = null;
        SetBaseServiceUriApi(sutProvider);
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.Sut.Url
            .RouteUrl(Arg.Is<UrlRouteContext>(c => c.RouteName == "TeamsIntegration_Create"))
            .Returns("https://localhost");
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integration.Id)
            .Returns(integration);
        sutProvider.GetDependency<ITeamsService>()
            .ObtainTokenViaOAuth(_validTeamsCode, Arg.Any<string>())
            .Returns(string.Empty);
        var state = IntegrationOAuthState.FromIntegration(integration, sutProvider.GetDependency<TimeProvider>());

        await Assert.ThrowsAsync<BadRequestException>(async () => await sutProvider.Sut.CreateAsync(_validTeamsCode, state.ToString()));
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_StateEmpty_ThrowsNotFound(
        SutProvider<TeamsIntegrationController> sutProvider)
    {
        SetBaseServiceUriApi(sutProvider);
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.Sut.Url
            .RouteUrl(Arg.Is<UrlRouteContext>(c => c.RouteName == "TeamsIntegration_Create"))
            .Returns("https://localhost");
        sutProvider.GetDependency<ITeamsService>()
            .ObtainTokenViaOAuth(_validTeamsCode, Arg.Any<string>())
            .Returns(_teamsToken);

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.CreateAsync(_validTeamsCode, string.Empty));
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_StateExpired_ThrowsNotFound(
        SutProvider<TeamsIntegrationController> sutProvider,
        OrganizationIntegration integration)
    {
        var timeProvider = new FakeTimeProvider(new DateTime(2024, 4, 3, 2, 1, 0, DateTimeKind.Utc));
        SetBaseServiceUriApi(sutProvider);
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.Sut.Url
            .RouteUrl(Arg.Is<UrlRouteContext>(c => c.RouteName == "TeamsIntegration_Create"))
            .Returns("https://localhost");
        sutProvider.GetDependency<ITeamsService>()
            .ObtainTokenViaOAuth(_validTeamsCode, Arg.Any<string>())
            .Returns(_teamsToken);
        var state = IntegrationOAuthState.FromIntegration(integration, timeProvider);
        timeProvider.Advance(TimeSpan.FromMinutes(30));

        sutProvider.SetDependency<TimeProvider>(timeProvider);
        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.CreateAsync(_validTeamsCode, state.ToString()));
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_StateHasNonexistentIntegration_ThrowsNotFound(
        SutProvider<TeamsIntegrationController> sutProvider,
        OrganizationIntegration integration)
    {
        SetBaseServiceUriApi(sutProvider);
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.Sut.Url
            .RouteUrl(Arg.Is<UrlRouteContext>(c => c.RouteName == "TeamsIntegration_Create"))
            .Returns("https://localhost");
        sutProvider.GetDependency<ITeamsService>()
            .ObtainTokenViaOAuth(_validTeamsCode, Arg.Any<string>())
            .Returns(_teamsToken);

        var state = IntegrationOAuthState.FromIntegration(integration, sutProvider.GetDependency<TimeProvider>());

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.CreateAsync(_validTeamsCode, state.ToString()));
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_StateHasWrongOrganizationHash_ThrowsNotFound(
        SutProvider<TeamsIntegrationController> sutProvider,
        OrganizationIntegration integration,
        OrganizationIntegration wrongOrgIntegration)
    {
        wrongOrgIntegration.Id = integration.Id;
        wrongOrgIntegration.Type = IntegrationType.Teams;
        wrongOrgIntegration.Configuration = null;

        SetBaseServiceUriApi(sutProvider);
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.Sut.Url
            .RouteUrl(Arg.Is<UrlRouteContext>(c => c.RouteName == "TeamsIntegration_Create"))
            .Returns("https://localhost");
        sutProvider.GetDependency<ITeamsService>()
            .ObtainTokenViaOAuth(_validTeamsCode, Arg.Any<string>())
            .Returns(_teamsToken);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integration.Id)
            .Returns(wrongOrgIntegration);

        var state = IntegrationOAuthState.FromIntegration(integration, sutProvider.GetDependency<TimeProvider>());

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.CreateAsync(_validTeamsCode, state.ToString()));
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_StateHasNonEmptyIntegration_ThrowsNotFound(
        SutProvider<TeamsIntegrationController> sutProvider,
        OrganizationIntegration integration)
    {
        integration.Type = IntegrationType.Teams;
        integration.Configuration = "{}";
        SetBaseServiceUriApi(sutProvider);
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.Sut.Url
            .RouteUrl(Arg.Is<UrlRouteContext>(c => c.RouteName == "TeamsIntegration_Create"))
            .Returns("https://localhost");
        sutProvider.GetDependency<ITeamsService>()
            .ObtainTokenViaOAuth(_validTeamsCode, Arg.Any<string>())
            .Returns(_teamsToken);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integration.Id)
            .Returns(integration);

        var state = IntegrationOAuthState.FromIntegration(integration, sutProvider.GetDependency<TimeProvider>());
        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.CreateAsync(_validTeamsCode, state.ToString()));
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_StateHasNonTeamsIntegration_ThrowsNotFound(
        SutProvider<TeamsIntegrationController> sutProvider,
        OrganizationIntegration integration)
    {
        integration.Type = IntegrationType.Hec;
        integration.Configuration = null;
        SetBaseServiceUriApi(sutProvider);
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.Sut.Url
            .RouteUrl(Arg.Is<UrlRouteContext>(c => c.RouteName == "TeamsIntegration_Create"))
            .Returns("https://localhost");
        sutProvider.GetDependency<ITeamsService>()
            .ObtainTokenViaOAuth(_validTeamsCode, Arg.Any<string>())
            .Returns(_teamsToken);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integration.Id)
            .Returns(integration);

        var state = IntegrationOAuthState.FromIntegration(integration, sutProvider.GetDependency<TimeProvider>());
        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.CreateAsync(_validTeamsCode, state.ToString()));
    }

    [Theory, BitAutoData]
    public async Task RedirectAsync_Success(
        SutProvider<TeamsIntegrationController> sutProvider,
        OrganizationIntegration integration)
    {
        integration.Configuration = null;
        var expectedUrl = "https://localhost/";

        SetBaseServiceUriApi(sutProvider);
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.Sut.Url
            .RouteUrl(Arg.Is<UrlRouteContext>(c => c.RouteName == "TeamsIntegration_Create"))
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
        sutProvider.GetDependency<ITeamsService>().GetRedirectUrl(Arg.Any<string>(), Arg.Any<string>()).Returns(expectedUrl);

        var expectedState = IntegrationOAuthState.FromIntegration(integration, sutProvider.GetDependency<TimeProvider>());

        var requestAction = await sutProvider.Sut.RedirectAsync(integration.OrganizationId);

        Assert.IsType<RedirectResult>(requestAction);
        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().Received(1)
            .CreateAsync(Arg.Any<OrganizationIntegration>());
        sutProvider.GetDependency<ITeamsService>().Received(1).GetRedirectUrl(Arg.Any<string>(), expectedState.ToString());
    }

    [Theory, BitAutoData]
    public async Task RedirectAsync_IntegrationAlreadyExistsWithNullConfig_Success(
        SutProvider<TeamsIntegrationController> sutProvider,
        Guid organizationId,
        OrganizationIntegration integration)
    {
        integration.OrganizationId = organizationId;
        integration.Configuration = null;
        integration.Type = IntegrationType.Teams;
        var expectedUrl = "https://localhost/";

        SetBaseServiceUriApi(sutProvider);
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.Sut.Url
            .RouteUrl(Arg.Is<UrlRouteContext>(c => c.RouteName == "TeamsIntegration_Create"))
            .Returns(expectedUrl);
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetManyByOrganizationAsync(organizationId)
            .Returns([integration]);
        sutProvider.GetDependency<ITeamsService>().GetRedirectUrl(Arg.Any<string>(), Arg.Any<string>()).Returns(expectedUrl);

        var requestAction = await sutProvider.Sut.RedirectAsync(organizationId);

        var expectedState = IntegrationOAuthState.FromIntegration(integration, sutProvider.GetDependency<TimeProvider>());

        Assert.IsType<RedirectResult>(requestAction);
        sutProvider.GetDependency<ITeamsService>().Received(1).GetRedirectUrl(Arg.Any<string>(), expectedState.ToString());
    }

    [Theory, BitAutoData]
    public async Task RedirectAsync_CallbackUrlIsEmpty_ThrowsBadRequest(
        SutProvider<TeamsIntegrationController> sutProvider,
        Guid organizationId,
        OrganizationIntegration integration)
    {
        integration.OrganizationId = organizationId;
        integration.Configuration = null;
        integration.Type = IntegrationType.Teams;

        SetBaseServiceUriApi(sutProvider);
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.Sut.Url
            .RouteUrl(Arg.Is<UrlRouteContext>(c => c.RouteName == "TeamsIntegration_Create"))
            .Returns((string?)null);
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetManyByOrganizationAsync(organizationId)
            .Returns([integration]);

        await Assert.ThrowsAsync<BadRequestException>(async () => await sutProvider.Sut.RedirectAsync(organizationId));
    }

    [Theory, BitAutoData]
    public async Task RedirectAsync_IntegrationAlreadyExistsWithConfig_ThrowsBadRequest(
        SutProvider<TeamsIntegrationController> sutProvider,
        Guid organizationId,
        OrganizationIntegration integration)
    {
        integration.OrganizationId = organizationId;
        integration.Configuration = "{}";
        integration.Type = IntegrationType.Teams;
        var expectedUrl = "https://localhost/";

        SetBaseServiceUriApi(sutProvider);
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.Sut.Url
            .RouteUrl(Arg.Is<UrlRouteContext>(c => c.RouteName == "TeamsIntegration_Create"))
            .Returns(expectedUrl);
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetManyByOrganizationAsync(organizationId)
            .Returns([integration]);
        sutProvider.GetDependency<ITeamsService>().GetRedirectUrl(Arg.Any<string>(), Arg.Any<string>()).Returns(expectedUrl);

        await Assert.ThrowsAsync<BadRequestException>(async () => await sutProvider.Sut.RedirectAsync(organizationId));
    }

    [Theory, BitAutoData]
    public async Task RedirectAsync_TeamsServiceReturnsEmpty_ThrowsNotFound(
        SutProvider<TeamsIntegrationController> sutProvider,
        Guid organizationId,
        OrganizationIntegration integration)
    {
        integration.OrganizationId = organizationId;
        integration.Configuration = null;
        var expectedUrl = "https://localhost/";

        SetBaseServiceUriApi(sutProvider);
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.Sut.Url
            .RouteUrl(Arg.Is<UrlRouteContext>(c => c.RouteName == "TeamsIntegration_Create"))
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
        sutProvider.GetDependency<ITeamsService>().GetRedirectUrl(Arg.Any<string>(), Arg.Any<string>()).Returns(string.Empty);

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.RedirectAsync(organizationId));
    }

    [Theory, BitAutoData]
    public async Task RedirectAsync_UserIsNotOrganizationAdmin_ThrowsNotFound(SutProvider<TeamsIntegrationController> sutProvider,
        Guid organizationId)
    {
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.RedirectAsync(organizationId));
    }

    [Theory, BitAutoData]
    public async Task IncomingPostAsync_ForwardsToBot(SutProvider<TeamsIntegrationController> sutProvider)
    {
        var adapter = sutProvider.GetDependency<IBotFrameworkHttpAdapter>();
        var bot = sutProvider.GetDependency<IBot>();

        await sutProvider.Sut.IncomingPostAsync();
        await adapter.Received(1).ProcessAsync(Arg.Any<HttpRequest>(), Arg.Any<HttpResponse>(), bot);
    }

    [Theory]
    [BitAutoData("https://api.example.com", "https://api.example.com/organizations/integrations/teams/create")]
    [BitAutoData("https://bitwarden.example.com/api/", "https://bitwarden.example.com/api/organizations/integrations/teams/create")]
    public async Task RedirectAsync_CallbackUrlUsesConfiguredApiBaseUrl(
        string apiBaseUrl,
        string expectedCallbackUrl,
        SutProvider<TeamsIntegrationController> sutProvider,
        OrganizationIntegration integration)
    {
        integration.Configuration = null;
        sutProvider.GetDependency<IGlobalSettings>().BaseServiceUri.Api.Returns(apiBaseUrl);
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.Sut.Url
            .RouteUrl(Arg.Is<UrlRouteContext>(c => c.RouteName == "TeamsIntegration_Create"))
            .Returns("/organizations/integrations/teams/create");
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(integration.OrganizationId)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetManyByOrganizationAsync(integration.OrganizationId)
            .Returns([]);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .CreateAsync(Arg.Any<OrganizationIntegration>())
            .Returns(integration);
        sutProvider.GetDependency<ITeamsService>()
            .GetRedirectUrl(expectedCallbackUrl, Arg.Any<string>())
            .Returns("https://teams.example.com/authorize");

        var requestAction = await sutProvider.Sut.RedirectAsync(integration.OrganizationId);

        Assert.IsType<RedirectResult>(requestAction);
        sutProvider.GetDependency<ITeamsService>().Received(1)
            .GetRedirectUrl(expectedCallbackUrl, Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task RedirectAsync_ApiBaseUriInvalid_ThrowsBadRequest(
        SutProvider<TeamsIntegrationController> sutProvider,
        Guid organizationId)
    {
        sutProvider.GetDependency<IGlobalSettings>().BaseServiceUri.Api.Returns(string.Empty);
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.Sut.Url
            .RouteUrl(Arg.Any<UrlRouteContext>())
            .Returns("/organizations/integrations/teams/create");
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(true);

        await Assert.ThrowsAsync<BadRequestException>(async () => await sutProvider.Sut.RedirectAsync(organizationId));
    }

    [Theory, BitAutoData]
    public async Task GetChannelsAsync_CompletedIntegration_ReturnsStandardChannels(
        SutProvider<TeamsIntegrationController> sutProvider,
        OrganizationIntegration integration)
    {
        var serviceUrl = new Uri("https://smba.example.com/amer/tenant/");
        SetupCompletedTeamsIntegration(integration, "19:team@thread.tacv2", serviceUrl);
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(integration.OrganizationId)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integration.Id)
            .Returns(integration);
        sutProvider.GetDependency<ITeamsService>()
            .GetStandardChannelsAsync(serviceUrl, "19:team@thread.tacv2")
            .Returns([
                new TeamsChannel { Id = "19:team@thread.tacv2", Name = null, Type = "standard" },
                new TeamsChannel { Id = "19:alerts@thread.tacv2", Name = "Alerts", Type = "standard" }
            ]);

        var result = await sutProvider.Sut.GetChannelsAsync(integration.OrganizationId, integration.Id);

        Assert.Collection(result.Data,
            general =>
            {
                Assert.Equal("19:team@thread.tacv2", general.Id);
                Assert.Null(general.Name);
            },
            alerts =>
            {
                Assert.Equal("19:alerts@thread.tacv2", alerts.Id);
                Assert.Equal("Alerts", alerts.Name);
            });
    }

    [Theory, BitAutoData]
    public async Task GetChannelsAsync_NotOrganizationOwner_ThrowsNotFound(
        SutProvider<TeamsIntegrationController> sutProvider,
        Guid organizationId,
        Guid integrationId)
    {
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(
            async () => await sutProvider.Sut.GetChannelsAsync(organizationId, integrationId));
        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().DidNotReceiveWithAnyArgs()
            .GetByIdAsync(default);
    }

    [Theory, BitAutoData]
    public async Task GetChannelsAsync_IntegrationNotFound_ThrowsNotFound(
        SutProvider<TeamsIntegrationController> sutProvider,
        Guid organizationId,
        Guid integrationId)
    {
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integrationId)
            .Returns((OrganizationIntegration?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            async () => await sutProvider.Sut.GetChannelsAsync(organizationId, integrationId));
    }

    [Theory, BitAutoData]
    public async Task GetChannelsAsync_IntegrationInOtherOrganization_ThrowsNotFound(
        SutProvider<TeamsIntegrationController> sutProvider,
        OrganizationIntegration integration,
        Guid otherOrganizationId)
    {
        SetupCompletedTeamsIntegration(integration, "19:team@thread.tacv2", new Uri("https://smba.example.com/"));
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(otherOrganizationId)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integration.Id)
            .Returns(integration);

        await Assert.ThrowsAsync<NotFoundException>(
            async () => await sutProvider.Sut.GetChannelsAsync(otherOrganizationId, integration.Id));
        await sutProvider.GetDependency<ITeamsService>().DidNotReceiveWithAnyArgs()
            .GetStandardChannelsAsync(default!, default!);
    }

    [Theory, BitAutoData]
    public async Task GetChannelsAsync_NonTeamsIntegration_ThrowsNotFound(
        SutProvider<TeamsIntegrationController> sutProvider,
        OrganizationIntegration integration)
    {
        integration.Type = IntegrationType.Slack;
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(integration.OrganizationId)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integration.Id)
            .Returns(integration);

        await Assert.ThrowsAsync<NotFoundException>(
            async () => await sutProvider.Sut.GetChannelsAsync(integration.OrganizationId, integration.Id));
    }

    [Theory, BitAutoData]
    public async Task GetChannelsAsync_IntegrationNotCompleted_ThrowsBadRequest(
        SutProvider<TeamsIntegrationController> sutProvider,
        OrganizationIntegration integration)
    {
        integration.Type = IntegrationType.Teams;
        integration.Configuration = JsonSerializer.Serialize(new TeamsIntegration(TenantId: "tenant", Teams: []));
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(integration.OrganizationId)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integration.Id)
            .Returns(integration);

        await Assert.ThrowsAsync<BadRequestException>(
            async () => await sutProvider.Sut.GetChannelsAsync(integration.OrganizationId, integration.Id));
    }

    [Theory, BitAutoData]
    public async Task GetChannelsAsync_IntegrationInitiated_ThrowsBadRequest(
        SutProvider<TeamsIntegrationController> sutProvider,
        OrganizationIntegration integration)
    {
        integration.Type = IntegrationType.Teams;
        integration.Configuration = null;
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(integration.OrganizationId)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integration.Id)
            .Returns(integration);

        await Assert.ThrowsAsync<BadRequestException>(
            async () => await sutProvider.Sut.GetChannelsAsync(integration.OrganizationId, integration.Id));
    }

    private static void SetupCompletedTeamsIntegration(OrganizationIntegration integration, string teamId, Uri serviceUrl)
    {
        integration.Type = IntegrationType.Teams;
        integration.Configuration = JsonSerializer.Serialize(new TeamsIntegration(
            TenantId: "tenant",
            Teams: [],
            ChannelId: teamId,
            ServiceUrl: serviceUrl));
    }

    private static void SetBaseServiceUriApi(SutProvider<TeamsIntegrationController> sutProvider)
    {
        sutProvider.GetDependency<IGlobalSettings>().BaseServiceUri.Api.Returns("https://api.example.com");
    }
}
