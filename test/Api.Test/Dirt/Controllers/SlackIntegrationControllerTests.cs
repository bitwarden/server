#nullable enable

using Bit.Api.Dirt.Controllers;
using Bit.Api.Dirt.Utilities;
using Bit.Core.Context;
using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Enums;
using Bit.Core.Dirt.Models.Data.EventIntegrations;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Dirt.Services;
using Bit.Core.Exceptions;
using Bit.Core.Settings;
using Bit.Core.Tokens;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Dirt.Controllers;

[ControllerCustomize(typeof(SlackIntegrationController))]
[SutProviderCustomize]
public class SlackIntegrationControllerTests
{
    private const string _slackToken = "xoxb-test-token";
    private const string _validSlackCode = "A_test_code";
    private const string _nonce = "test-nonce";

    [Theory, BitAutoData]
    public async Task CreateAsync_AllParamsProvided_Succeeds(
        SutProvider<SlackIntegrationController> sutProvider,
        OrganizationIntegration integration)
    {
        integration.Type = IntegrationType.Slack;
        integration.Configuration = null;
        SetBaseServiceUriApi(sutProvider);
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

        var state = SetupState(sutProvider, integration);
        var requestAction = await sutProvider.Sut.CreateAsync(_validSlackCode, state, null);

        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().Received(1)
            .UpsertAsync(Arg.Any<OrganizationIntegration>());
        AssertRedirectsToVault(requestAction, integration.OrganizationId, IntegrationOAuthCallbackRedirect.Success);
        await sutProvider.GetDependency<IIntegrationOAuthNonceStore>().Received(1)
            .TryConsumeAsync(integration.Id, _nonce);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_CodeIsEmpty_RedirectsWithCanceled(
        SutProvider<SlackIntegrationController> sutProvider,
        OrganizationIntegration integration)
    {
        integration.Type = IntegrationType.Slack;
        integration.Configuration = null;
        SetBaseServiceUriApi(sutProvider);
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.Sut.Url
            .RouteUrl(Arg.Is<UrlRouteContext>(c => c.RouteName == "SlackIntegration_Create"))
            .Returns("https://localhost");
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integration.Id)
            .Returns(integration);
        var state = SetupState(sutProvider, integration);

        var requestAction = await sutProvider.Sut.CreateAsync(string.Empty, state, null);

        AssertRedirectsToVault(requestAction, integration.OrganizationId, IntegrationOAuthCallbackRedirect.Canceled);
        await sutProvider.GetDependency<IIntegrationOAuthNonceStore>().Received(1)
            .TryConsumeAsync(integration.Id, _nonce);
        await sutProvider.GetDependency<ISlackService>().DidNotReceiveWithAnyArgs()
            .ObtainTokenViaOAuth(default!, default!);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_CallbackUrlIsEmpty_RedirectsWithFailed(
        SutProvider<SlackIntegrationController> sutProvider,
        OrganizationIntegration integration)
    {
        integration.Type = IntegrationType.Slack;
        integration.Configuration = null;
        SetBaseServiceUriApi(sutProvider);
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.Sut.Url
            .RouteUrl(Arg.Is<UrlRouteContext>(c => c.RouteName == "SlackIntegration_Create"))
            .Returns((string?)null);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integration.Id)
            .Returns(integration);
        var state = SetupState(sutProvider, integration);

        var requestAction = await sutProvider.Sut.CreateAsync(_validSlackCode, state, null);

        AssertRedirectsToVault(requestAction, integration.OrganizationId, IntegrationOAuthCallbackRedirect.Failed);
        await sutProvider.GetDependency<IIntegrationOAuthNonceStore>().Received(1)
            .TryConsumeAsync(integration.Id, _nonce);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_SlackServiceReturnsEmpty_RedirectsWithFailed(
        SutProvider<SlackIntegrationController> sutProvider,
        OrganizationIntegration integration)
    {
        integration.Type = IntegrationType.Slack;
        integration.Configuration = null;
        SetBaseServiceUriApi(sutProvider);
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
        var state = SetupState(sutProvider, integration);

        var requestAction = await sutProvider.Sut.CreateAsync(_validSlackCode, state, null);

        AssertRedirectsToVault(requestAction, integration.OrganizationId, IntegrationOAuthCallbackRedirect.Failed);
        await sutProvider.GetDependency<IIntegrationOAuthNonceStore>().Received(1)
            .TryConsumeAsync(integration.Id, _nonce);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_ProviderReturnsError_RedirectsWithCanceled(
        SutProvider<SlackIntegrationController> sutProvider,
        OrganizationIntegration integration)
    {
        integration.Type = IntegrationType.Slack;
        integration.Configuration = null;
        SetBaseServiceUriApi(sutProvider);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integration.Id)
            .Returns(integration);
        var state = SetupState(sutProvider, integration);

        var requestAction = await sutProvider.Sut.CreateAsync(null, state, "access_denied");

        AssertRedirectsToVault(requestAction, integration.OrganizationId, IntegrationOAuthCallbackRedirect.Canceled);
        await sutProvider.GetDependency<IIntegrationOAuthNonceStore>().Received(1)
            .TryConsumeAsync(integration.Id, _nonce);
        await sutProvider.GetDependency<ISlackService>().DidNotReceiveWithAnyArgs()
            .ObtainTokenViaOAuth(default!, default!);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_StateEmpty_ThrowsNotFound(
        SutProvider<SlackIntegrationController> sutProvider)
    {
        SetBaseServiceUriApi(sutProvider);
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.Sut.Url
            .RouteUrl(Arg.Is<UrlRouteContext>(c => c.RouteName == "SlackIntegration_Create"))
            .Returns("https://localhost");
        sutProvider.GetDependency<ISlackService>()
            .ObtainTokenViaOAuth(_validSlackCode, Arg.Any<string>())
            .Returns(_slackToken);

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.CreateAsync(_validSlackCode, string.Empty, null));
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_StateCannotBeUnprotected_ThrowsNotFound(
        SutProvider<SlackIntegrationController> sutProvider,
        OrganizationIntegration integration)
    {
        // A state forged from known IDs (the old unkeyed format) fails Data Protection's MAC check
        var forgedState = $"{integration.Id}.ABCDEF123456.{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        sutProvider.GetDependency<IDataProtectorTokenFactory<IntegrationOAuthStateTokenable>>()
            .TryUnprotect(forgedState, out Arg.Any<IntegrationOAuthStateTokenable>())
            .Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.CreateAsync(_validSlackCode, forgedState, null));

        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().DidNotReceiveWithAnyArgs()
            .GetByIdAsync(default);
        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().DidNotReceiveWithAnyArgs()
            .UpsertAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_StateExpired_ThrowsNotFound(
        SutProvider<SlackIntegrationController> sutProvider,
        OrganizationIntegration integration)
    {
        integration.Type = IntegrationType.Slack;
        integration.Configuration = null;
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integration.Id)
            .Returns(integration);
        var state = SetupState(sutProvider, new IntegrationOAuthStateTokenable(integration, _nonce, TimeProvider.System)
        {
            ExpirationDate = DateTime.UtcNow.AddMinutes(-1)
        });

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.CreateAsync(_validSlackCode, state, null));

        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().DidNotReceiveWithAnyArgs()
            .UpsertAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_StateHasNonexistentIntegration_ThrowsNotFound(
        SutProvider<SlackIntegrationController> sutProvider,
        OrganizationIntegration integration)
    {
        integration.Type = IntegrationType.Slack;
        SetBaseServiceUriApi(sutProvider);
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.Sut.Url
            .RouteUrl(Arg.Is<UrlRouteContext>(c => c.RouteName == "SlackIntegration_Create"))
            .Returns("https://localhost");
        sutProvider.GetDependency<ISlackService>()
            .ObtainTokenViaOAuth(_validSlackCode, Arg.Any<string>())
            .Returns(_slackToken);

        var state = SetupState(sutProvider, integration);

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.CreateAsync(_validSlackCode, state, null));
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_StateHasWrongOrganization_ThrowsNotFound(
        SutProvider<SlackIntegrationController> sutProvider,
        OrganizationIntegration integration,
        OrganizationIntegration wrongOrgIntegration)
    {
        integration.Type = IntegrationType.Slack;
        wrongOrgIntegration.Id = integration.Id;
        wrongOrgIntegration.Type = IntegrationType.Slack;
        wrongOrgIntegration.Configuration = null;

        SetBaseServiceUriApi(sutProvider);
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

        var state = SetupState(sutProvider, integration);

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.CreateAsync(_validSlackCode, state, null));
        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().DidNotReceiveWithAnyArgs()
            .UpsertAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_StateHasNonEmptyIntegration_RedirectsWithAlreadyConnected(
        SutProvider<SlackIntegrationController> sutProvider,
        OrganizationIntegration integration)
    {
        integration.Type = IntegrationType.Slack;
        integration.Configuration = "{}";
        SetBaseServiceUriApi(sutProvider);
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

        var state = SetupState(sutProvider, integration);
        var requestAction = await sutProvider.Sut.CreateAsync(_validSlackCode, state, null);

        AssertRedirectsToVault(requestAction, integration.OrganizationId, IntegrationOAuthCallbackRedirect.AlreadyConnected);
        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().DidNotReceiveWithAnyArgs()
            .UpsertAsync(default!);
        await sutProvider.GetDependency<IIntegrationOAuthNonceStore>().DidNotReceiveWithAnyArgs()
            .TryConsumeAsync(default, default!);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_NonceAlreadyConsumedOrReplaced_ThrowsNotFound(
        SutProvider<SlackIntegrationController> sutProvider,
        OrganizationIntegration integration)
    {
        integration.Type = IntegrationType.Slack;
        integration.Configuration = null;
        SetBaseServiceUriApi(sutProvider);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integration.Id)
            .Returns(integration);
        var state = SetupState(sutProvider, integration);
        sutProvider.GetDependency<IIntegrationOAuthNonceStore>()
            .TryConsumeAsync(integration.Id, _nonce)
            .Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.CreateAsync(_validSlackCode, state, null));

        await sutProvider.GetDependency<ISlackService>().DidNotReceiveWithAnyArgs()
            .ObtainTokenViaOAuth(default!, default!);
        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().DidNotReceiveWithAnyArgs()
            .UpsertAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_StateHasNonSlackIntegration_ThrowsNotFound(
        SutProvider<SlackIntegrationController> sutProvider,
        OrganizationIntegration integration)
    {
        integration.Type = IntegrationType.Hec;
        integration.Configuration = null;
        SetBaseServiceUriApi(sutProvider);
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

        var state = SetupState(sutProvider, integration);
        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.CreateAsync(_validSlackCode, state, null));
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_StateIssuedForTeams_ThrowsNotFound(
        SutProvider<SlackIntegrationController> sutProvider,
        OrganizationIntegration integration)
    {
        integration.Type = IntegrationType.Teams;
        integration.Configuration = null;
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integration.Id)
            .Returns(integration);

        var state = SetupState(sutProvider, integration);

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.CreateAsync(_validSlackCode, state, null));
        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().DidNotReceiveWithAnyArgs()
            .GetByIdAsync(default);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_StateTypeDoesNotMatchIntegration_ThrowsNotFound(
        SutProvider<SlackIntegrationController> sutProvider,
        OrganizationIntegration integration)
    {
        integration.Type = IntegrationType.Slack;
        var state = SetupState(sutProvider, integration);

        // The stored record is not a Slack integration even though the state claims it is
        integration.Type = IntegrationType.Teams;
        integration.Configuration = null;
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integration.Id)
            .Returns(integration);

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.CreateAsync(_validSlackCode, state, null));
    }

    [Theory, BitAutoData]
    public async Task RedirectAsync_Success(
        SutProvider<SlackIntegrationController> sutProvider,
        OrganizationIntegration integration)
    {
        integration.Configuration = null;
        var expectedUrl = "https://localhost/";
        var fakeTime = new FakeTimeProvider(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        sutProvider.SetDependency<TimeProvider>(fakeTime, "timeProvider").Create();

        SetBaseServiceUriApi(sutProvider);
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
        var expectedState = SetupProtect(sutProvider, integration, fakeTime);

        var response = await sutProvider.Sut.RedirectAsync(integration.OrganizationId);

        Assert.Equal(expectedUrl, response.Url);
        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().Received(1)
            .CreateAsync(Arg.Any<OrganizationIntegration>());
        sutProvider.GetDependency<ISlackService>().Received(1).GetRedirectUrl(Arg.Any<string>(), expectedState);
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
        var fakeTime = new FakeTimeProvider(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        sutProvider.SetDependency<TimeProvider>(fakeTime, "timeProvider").Create();

        SetBaseServiceUriApi(sutProvider);
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
        var expectedState = SetupProtect(sutProvider, integration, fakeTime);

        var response = await sutProvider.Sut.RedirectAsync(organizationId);

        Assert.Equal(expectedUrl, response.Url);
        sutProvider.GetDependency<ISlackService>().Received(1).GetRedirectUrl(Arg.Any<string>(), expectedState);
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

        SetBaseServiceUriApi(sutProvider);
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
    public async Task RedirectAsync_CallbackUrlReturnsEmpty_ThrowsBadRequest(
        SutProvider<SlackIntegrationController> sutProvider,
        Guid organizationId)
    {
        SetBaseServiceUriApi(sutProvider);
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.Sut.Url
            .RouteUrl(Arg.Is<UrlRouteContext>(c => c.RouteName == "SlackIntegration_Create"))
            .Returns((string?)null);
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(true);

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

        SetBaseServiceUriApi(sutProvider);
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

    [Theory]
    [BitAutoData("https://api.example.com", "https://api.example.com/organizations/integrations/slack/create")]
    [BitAutoData("https://bitwarden.example.com/api/", "https://bitwarden.example.com/api/organizations/integrations/slack/create")]
    public async Task RedirectAsync_CallbackUrlUsesConfiguredApiBaseUrl(
        string apiBaseUrl,
        string expectedCallbackUrl,
        SutProvider<SlackIntegrationController> sutProvider,
        OrganizationIntegration integration)
    {
        integration.Configuration = null;
        sutProvider.GetDependency<IGlobalSettings>().BaseServiceUri.Api.Returns(apiBaseUrl);
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.Sut.Url
            .RouteUrl(Arg.Is<UrlRouteContext>(c => c.RouteName == "SlackIntegration_Create"))
            .Returns("/organizations/integrations/slack/create");
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(integration.OrganizationId)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetManyByOrganizationAsync(integration.OrganizationId)
            .Returns([]);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .CreateAsync(Arg.Any<OrganizationIntegration>())
            .Returns(integration);
        sutProvider.GetDependency<ISlackService>()
            .GetRedirectUrl(expectedCallbackUrl, Arg.Any<string>())
            .Returns("https://slack.example.com/authorize");

        var response = await sutProvider.Sut.RedirectAsync(integration.OrganizationId);

        Assert.Equal("https://slack.example.com/authorize", response.Url);
        sutProvider.GetDependency<ISlackService>().Received(1)
            .GetRedirectUrl(expectedCallbackUrl, Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task RedirectAsync_ApiBaseUriInvalid_ThrowsBadRequest(
        SutProvider<SlackIntegrationController> sutProvider,
        Guid organizationId)
    {
        sutProvider.GetDependency<IGlobalSettings>().BaseServiceUri.Api.Returns(string.Empty);
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.Sut.Url
            .RouteUrl(Arg.Any<UrlRouteContext>())
            .Returns("/organizations/integrations/slack/create");
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(true);

        await Assert.ThrowsAsync<BadRequestException>(async () => await sutProvider.Sut.RedirectAsync(organizationId));
    }

    private static string SetupState(SutProvider<SlackIntegrationController> sutProvider, OrganizationIntegration integration) =>
        SetupState(sutProvider, new IntegrationOAuthStateTokenable(integration, _nonce, TimeProvider.System));

    private static string SetupState(
        SutProvider<SlackIntegrationController> sutProvider,
        IntegrationOAuthStateTokenable tokenable)
    {
        const string state = "protected-oauth-state";
        sutProvider.GetDependency<IDataProtectorTokenFactory<IntegrationOAuthStateTokenable>>()
            .TryUnprotect(state, out Arg.Any<IntegrationOAuthStateTokenable>())
            .Returns(call =>
            {
                call[1] = tokenable;
                return true;
            });
        sutProvider.GetDependency<IIntegrationOAuthNonceStore>()
            .TryConsumeAsync(tokenable.IntegrationId, tokenable.Nonce)
            .Returns(true);
        return state;
    }

    private static string SetupProtect(
        SutProvider<SlackIntegrationController> sutProvider,
        OrganizationIntegration integration,
        TimeProvider timeProvider)
    {
        const string state = "protected-oauth-state";
        var expectedExpiration = timeProvider.GetUtcNow().UtcDateTime.Add(IntegrationOAuthStateTokenable.Lifetime);
        sutProvider.GetDependency<IIntegrationOAuthNonceStore>()
            .IssueAsync(integration.Id)
            .Returns(_nonce);
        sutProvider.GetDependency<IDataProtectorTokenFactory<IntegrationOAuthStateTokenable>>()
            .Protect(Arg.Is<IntegrationOAuthStateTokenable>(t =>
                t.IntegrationId == integration.Id &&
                t.OrganizationId == integration.OrganizationId &&
                t.Type == integration.Type &&
                t.Nonce == _nonce &&
                t.ExpirationDate == expectedExpiration))
            .Returns(state);
        return state;
    }

    private static void SetBaseServiceUriApi(SutProvider<SlackIntegrationController> sutProvider)
    {
        sutProvider.GetDependency<IGlobalSettings>().BaseServiceUri.Api.Returns("https://api.example.com");
        sutProvider.GetDependency<IGlobalSettings>().BaseServiceUri.VaultWithHash.Returns("https://vault.example.com/#");
    }

    private static void AssertRedirectsToVault(IActionResult actionResult, Guid organizationId, string expectedResult)
    {
        var redirect = Assert.IsType<RedirectResult>(actionResult);
        Assert.Equal(
            $"https://vault.example.com/#/organizations/{organizationId}/integrations/event-management" +
            $"?integration=slack&result={expectedResult}",
            redirect.Url);
    }
}
