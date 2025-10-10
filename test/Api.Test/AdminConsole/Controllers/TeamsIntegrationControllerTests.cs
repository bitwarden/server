#nullable enable

using Bit.Api.AdminConsole.Controllers;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Data.EventIntegrations;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Teams;
using Bit.Core.Repositories;
using Bit.Core.Services;
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

namespace Bit.Api.Test.AdminConsole.Controllers;

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
    public async Task CreateAsync_CodeIsEmpty_ThrowsBadRequest(
        SutProvider<TeamsIntegrationController> sutProvider,
        OrganizationIntegration integration)
    {
        integration.Type = IntegrationType.Teams;
        integration.Configuration = null;
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
    public async Task RedirectAsync_IntegrationAlreadyExistsWithConfig_ThrowsBadRequest(
        SutProvider<TeamsIntegrationController> sutProvider,
        Guid organizationId,
        OrganizationIntegration integration)
    {
        integration.OrganizationId = organizationId;
        integration.Configuration = "{}";
        integration.Type = IntegrationType.Teams;
        var expectedUrl = "https://localhost/";

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
}
