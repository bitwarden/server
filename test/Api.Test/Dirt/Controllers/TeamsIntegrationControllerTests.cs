#nullable enable

using System.Text.Json;
using Bit.Api.Dirt.Controllers;
using Bit.Api.Dirt.Models.Request;
using Bit.Api.Dirt.Utilities;
using Bit.Core.Context;
using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Enums;
using Bit.Core.Dirt.Models.Data.EventIntegrations;
using Bit.Core.Dirt.Models.Data.Teams;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Dirt.Services;
using Bit.Core.Exceptions;
using Bit.Core.Settings;
using Bit.Core.Tokens;
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
    private const string _nonce = "test-nonce";
    private const string _callbackUrl = "https://api.example.com/organizations/integrations/teams/create";

    [Theory, BitAutoData]
    public async Task CreateAsync_ValidCodeAndState_RedirectsToVaultWithCodeAndState(
        SutProvider<TeamsIntegrationController> sutProvider,
        OrganizationIntegration integration)
    {
        integration.Type = IntegrationType.Teams;
        integration.Configuration = null;
        SetBaseServiceUriApi(sutProvider);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integration.Id)
            .Returns(integration);
        var state = SetupState(sutProvider, integration);

        var requestAction = await sutProvider.Sut.CreateAsync(_validTeamsCode, state, null);

        var redirect = Assert.IsType<RedirectResult>(requestAction);
        Assert.Equal(
            $"https://vault.example.com/#/organizations/{integration.OrganizationId}/integrations/event-management" +
            $"?integration=teams&result={IntegrationOAuthCallbackRedirect.Authorized}" +
            $"&code={Uri.EscapeDataString(_validTeamsCode)}&state={Uri.EscapeDataString(state)}",
            redirect.Url);
        await AssertNoSideEffects(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_CodeAndStateNeedEscaping_AreUrlEncoded(
        SutProvider<TeamsIntegrationController> sutProvider,
        OrganizationIntegration integration)
    {
        const string code = "a+b/c=&d";
        integration.Type = IntegrationType.Teams;
        integration.Configuration = null;
        SetBaseServiceUriApi(sutProvider);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integration.Id)
            .Returns(integration);
        var state = SetupState(sutProvider, integration);

        var requestAction = await sutProvider.Sut.CreateAsync(code, state, null);

        var redirect = Assert.IsType<RedirectResult>(requestAction);
        Assert.EndsWith("&code=a%2Bb%2Fc%3D%26d&state=protected-oauth-state", redirect.Url);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_CodeIsEmpty_RedirectsWithCanceled(
        SutProvider<TeamsIntegrationController> sutProvider,
        OrganizationIntegration integration)
    {
        integration.Type = IntegrationType.Teams;
        integration.Configuration = null;
        SetBaseServiceUriApi(sutProvider);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integration.Id)
            .Returns(integration);
        var state = SetupState(sutProvider, integration);

        var requestAction = await sutProvider.Sut.CreateAsync(string.Empty, state, null);

        AssertRedirectsToVault(requestAction, integration.OrganizationId, IntegrationOAuthCallbackRedirect.Canceled);
        await AssertNoSideEffects(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_ProviderReturnsError_RedirectsWithCanceled(
        SutProvider<TeamsIntegrationController> sutProvider,
        OrganizationIntegration integration)
    {
        integration.Type = IntegrationType.Teams;
        integration.Configuration = null;
        SetBaseServiceUriApi(sutProvider);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integration.Id)
            .Returns(integration);
        var state = SetupState(sutProvider, integration);

        var requestAction = await sutProvider.Sut.CreateAsync(null, state, "access_denied");

        AssertRedirectsToVault(requestAction, integration.OrganizationId, IntegrationOAuthCallbackRedirect.Canceled);
        await AssertNoSideEffects(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_IntegrationAlreadyConfigured_RedirectsWithAlreadyConnected(
        SutProvider<TeamsIntegrationController> sutProvider,
        OrganizationIntegration integration)
    {
        integration.Type = IntegrationType.Teams;
        integration.Configuration = "{}";
        SetBaseServiceUriApi(sutProvider);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integration.Id)
            .Returns(integration);
        var state = SetupState(sutProvider, integration);

        var requestAction = await sutProvider.Sut.CreateAsync(_validTeamsCode, state, null);

        AssertRedirectsToVault(requestAction, integration.OrganizationId, IntegrationOAuthCallbackRedirect.AlreadyConnected);
        await AssertNoSideEffects(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_StateEmpty_ThrowsNotFound(
        SutProvider<TeamsIntegrationController> sutProvider)
    {
        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.CreateAsync(_validTeamsCode, string.Empty, null));

        await AssertNoSideEffects(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_StateCannotBeUnprotected_ThrowsNotFound(
        SutProvider<TeamsIntegrationController> sutProvider,
        OrganizationIntegration integration)
    {
        // A state forged from known IDs (the old unkeyed format) fails Data Protection's MAC check
        var forgedState = $"{integration.Id}.ABCDEF123456.{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        sutProvider.GetDependency<IDataProtectorTokenFactory<IntegrationOAuthStateTokenable>>()
            .TryUnprotect(forgedState, out Arg.Any<IntegrationOAuthStateTokenable>())
            .Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.CreateAsync(_validTeamsCode, forgedState, null));

        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().DidNotReceiveWithAnyArgs()
            .GetByIdAsync(default);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_StateExpired_ThrowsNotFound(
        SutProvider<TeamsIntegrationController> sutProvider,
        OrganizationIntegration integration)
    {
        integration.Type = IntegrationType.Teams;
        integration.Configuration = null;
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integration.Id)
            .Returns(integration);
        var state = SetupState(sutProvider, new IntegrationOAuthStateTokenable(integration, _nonce, TimeProvider.System)
        {
            ExpirationDate = DateTime.UtcNow.AddMinutes(-1)
        });

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.CreateAsync(_validTeamsCode, state, null));
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_StateHasNonexistentIntegration_ThrowsNotFound(
        SutProvider<TeamsIntegrationController> sutProvider,
        OrganizationIntegration integration)
    {
        integration.Type = IntegrationType.Teams;
        var state = SetupState(sutProvider, integration);

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.CreateAsync(_validTeamsCode, state, null));
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_StateHasWrongOrganization_ThrowsNotFound(
        SutProvider<TeamsIntegrationController> sutProvider,
        OrganizationIntegration integration,
        OrganizationIntegration wrongOrgIntegration)
    {
        integration.Type = IntegrationType.Teams;
        wrongOrgIntegration.Id = integration.Id;
        wrongOrgIntegration.Type = IntegrationType.Teams;
        wrongOrgIntegration.Configuration = null;
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integration.Id)
            .Returns(wrongOrgIntegration);
        var state = SetupState(sutProvider, integration);

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.CreateAsync(_validTeamsCode, state, null));
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_StateIssuedForSlack_ThrowsNotFound(
        SutProvider<TeamsIntegrationController> sutProvider,
        OrganizationIntegration integration)
    {
        integration.Type = IntegrationType.Slack;
        integration.Configuration = null;
        var state = SetupState(sutProvider, integration);

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.CreateAsync(_validTeamsCode, state, null));
        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().DidNotReceiveWithAnyArgs()
            .GetByIdAsync(default);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_StateTypeDoesNotMatchIntegration_ThrowsNotFound(
        SutProvider<TeamsIntegrationController> sutProvider,
        OrganizationIntegration integration)
    {
        integration.Type = IntegrationType.Teams;
        var state = SetupState(sutProvider, integration);

        // The stored record is not a Teams integration even though the state claims it is
        integration.Type = IntegrationType.Hec;
        integration.Configuration = null;
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integration.Id)
            .Returns(integration);

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.CreateAsync(_validTeamsCode, state, null));
    }

    [Theory, BitAutoData]
    public async Task CompleteAsync_ValidRequest_StoresIntegrationAndReturnsSuccess(
        SutProvider<TeamsIntegrationController> sutProvider,
        OrganizationIntegration integration)
    {
        var request = SetupComplete(sutProvider, integration);
        var team = new TeamInfo { DisplayName = "Test Team", Id = Guid.NewGuid().ToString(), TenantId = Guid.NewGuid().ToString() };
        sutProvider.GetDependency<ITeamsService>()
            .ObtainTokenViaOAuth(_validTeamsCode, _callbackUrl)
            .Returns(_teamsToken);
        sutProvider.GetDependency<ITeamsService>()
            .GetJoinedTeamsAsync(_teamsToken)
            .Returns([team]);

        var response = await sutProvider.Sut.CompleteAsync(integration.OrganizationId, request);

        Assert.Equal(IntegrationOAuthCallbackRedirect.Success, response.Result);
        await sutProvider.GetDependency<IIntegrationOAuthNonceStore>().Received(1)
            .TryConsumeAsync(integration.Id, _nonce);
        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().Received(1)
            .UpsertAsync(Arg.Is<OrganizationIntegration>(i =>
                i.Id == integration.Id &&
                i.Configuration != null &&
                JsonSerializer.Deserialize<TeamsIntegration>(i.Configuration, (JsonSerializerOptions?)null)!.TenantId == team.TenantId));
    }

    [Theory, BitAutoData]
    public async Task CompleteAsync_NotOrganizationOwner_ThrowsNotFound(
        SutProvider<TeamsIntegrationController> sutProvider,
        OrganizationIntegration integration)
    {
        // e.g. a victim who opened an authorize link generated by an attacker's organization
        var request = SetupComplete(sutProvider, integration);
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(integration.OrganizationId)
            .Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(async () =>
            await sutProvider.Sut.CompleteAsync(integration.OrganizationId, request));

        sutProvider.GetDependency<IDataProtectorTokenFactory<IntegrationOAuthStateTokenable>>().DidNotReceiveWithAnyArgs()
            .TryUnprotect(default!, out Arg.Any<IntegrationOAuthStateTokenable>());
        await AssertNoSideEffects(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task CompleteAsync_StateIssuedForAnotherOrganization_ThrowsNotFound(
        SutProvider<TeamsIntegrationController> sutProvider,
        OrganizationIntegration integration,
        Guid otherOrganizationId)
    {
        // An owner of one organization can't redeem a state issued for another organization
        var request = SetupComplete(sutProvider, integration);
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(otherOrganizationId)
            .Returns(true);

        await Assert.ThrowsAsync<NotFoundException>(async () =>
            await sutProvider.Sut.CompleteAsync(otherOrganizationId, request));

        await AssertNoSideEffects(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task CompleteAsync_StateCannotBeUnprotected_ThrowsNotFound(
        SutProvider<TeamsIntegrationController> sutProvider,
        OrganizationIntegration integration)
    {
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(integration.OrganizationId)
            .Returns(true);
        sutProvider.GetDependency<IDataProtectorTokenFactory<IntegrationOAuthStateTokenable>>()
            .TryUnprotect("forged-state", out Arg.Any<IntegrationOAuthStateTokenable>())
            .Returns(false);
        var request = new OrganizationIntegrationOAuthCompleteRequestModel { Code = _validTeamsCode, State = "forged-state" };

        await Assert.ThrowsAsync<NotFoundException>(async () =>
            await sutProvider.Sut.CompleteAsync(integration.OrganizationId, request));

        await AssertNoSideEffects(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task CompleteAsync_StateExpired_ThrowsNotFound(
        SutProvider<TeamsIntegrationController> sutProvider,
        OrganizationIntegration integration)
    {
        var request = SetupComplete(sutProvider, integration);
        SetupState(sutProvider, new IntegrationOAuthStateTokenable(integration, _nonce, TimeProvider.System)
        {
            ExpirationDate = DateTime.UtcNow.AddMinutes(-1)
        });

        await Assert.ThrowsAsync<NotFoundException>(async () =>
            await sutProvider.Sut.CompleteAsync(integration.OrganizationId, request));

        await AssertNoSideEffects(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task CompleteAsync_StateIssuedForSlack_ThrowsNotFound(
        SutProvider<TeamsIntegrationController> sutProvider,
        OrganizationIntegration integration)
    {
        var request = SetupComplete(sutProvider, integration);
        integration.Type = IntegrationType.Slack;
        SetupState(sutProvider, integration);

        await Assert.ThrowsAsync<NotFoundException>(async () =>
            await sutProvider.Sut.CompleteAsync(integration.OrganizationId, request));

        await AssertNoSideEffects(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task CompleteAsync_IntegrationAlreadyConfigured_ReturnsAlreadyConnected(
        SutProvider<TeamsIntegrationController> sutProvider,
        OrganizationIntegration integration)
    {
        var request = SetupComplete(sutProvider, integration);
        integration.Configuration = "{}";

        var response = await sutProvider.Sut.CompleteAsync(integration.OrganizationId, request);

        Assert.Equal(IntegrationOAuthCallbackRedirect.AlreadyConnected, response.Result);
        await AssertNoSideEffects(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task CompleteAsync_NonceAlreadyConsumedOrReplaced_ThrowsNotFound(
        SutProvider<TeamsIntegrationController> sutProvider,
        OrganizationIntegration integration)
    {
        var request = SetupComplete(sutProvider, integration);
        sutProvider.GetDependency<IIntegrationOAuthNonceStore>()
            .TryConsumeAsync(integration.Id, _nonce)
            .Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(async () =>
            await sutProvider.Sut.CompleteAsync(integration.OrganizationId, request));

        await sutProvider.GetDependency<ITeamsService>().DidNotReceiveWithAnyArgs()
            .ObtainTokenViaOAuth(default!, default!);
        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().DidNotReceiveWithAnyArgs()
            .UpsertAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task CompleteAsync_CallbackUrlIsEmpty_ReturnsFailed(
        SutProvider<TeamsIntegrationController> sutProvider,
        OrganizationIntegration integration)
    {
        var request = SetupComplete(sutProvider, integration);
        sutProvider.Sut.Url
            .RouteUrl(Arg.Is<UrlRouteContext>(c => c.RouteName == "TeamsIntegration_Create"))
            .Returns((string?)null);

        var response = await sutProvider.Sut.CompleteAsync(integration.OrganizationId, request);

        Assert.Equal(IntegrationOAuthCallbackRedirect.Failed, response.Result);
        await sutProvider.GetDependency<IIntegrationOAuthNonceStore>().Received(1)
            .TryConsumeAsync(integration.Id, _nonce);
        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().DidNotReceiveWithAnyArgs()
            .UpsertAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task CompleteAsync_TeamsServiceReturnsEmptyToken_ReturnsFailed(
        SutProvider<TeamsIntegrationController> sutProvider,
        OrganizationIntegration integration)
    {
        var request = SetupComplete(sutProvider, integration);
        sutProvider.GetDependency<ITeamsService>()
            .ObtainTokenViaOAuth(_validTeamsCode, _callbackUrl)
            .Returns(string.Empty);

        var response = await sutProvider.Sut.CompleteAsync(integration.OrganizationId, request);

        Assert.Equal(IntegrationOAuthCallbackRedirect.Failed, response.Result);
        await sutProvider.GetDependency<IIntegrationOAuthNonceStore>().Received(1)
            .TryConsumeAsync(integration.Id, _nonce);
        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().DidNotReceiveWithAnyArgs()
            .UpsertAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task CompleteAsync_NoTeamsFound_ReturnsNoTeams(
        SutProvider<TeamsIntegrationController> sutProvider,
        OrganizationIntegration integration)
    {
        var request = SetupComplete(sutProvider, integration);
        sutProvider.GetDependency<ITeamsService>()
            .ObtainTokenViaOAuth(_validTeamsCode, _callbackUrl)
            .Returns(_teamsToken);
        sutProvider.GetDependency<ITeamsService>()
            .GetJoinedTeamsAsync(_teamsToken)
            .Returns([]);

        var response = await sutProvider.Sut.CompleteAsync(integration.OrganizationId, request);

        Assert.Equal(IntegrationOAuthCallbackRedirect.NoTeams, response.Result);
        await sutProvider.GetDependency<IIntegrationOAuthNonceStore>().Received(1)
            .TryConsumeAsync(integration.Id, _nonce);
        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().DidNotReceiveWithAnyArgs()
            .UpsertAsync(default!);
    }


    [Theory, BitAutoData]
    public async Task RedirectAsync_Success(
        SutProvider<TeamsIntegrationController> sutProvider,
        OrganizationIntegration integration)
    {
        integration.Configuration = null;
        var expectedUrl = "https://localhost/";
        var fakeTime = new FakeTimeProvider(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        sutProvider.SetDependency<TimeProvider>(fakeTime, "timeProvider").Create();

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

        var expectedState = SetupProtect(sutProvider, integration, fakeTime);

        var response = await sutProvider.Sut.RedirectAsync(integration.OrganizationId);

        Assert.Equal(expectedUrl, response.Url);
        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().Received(1)
            .CreateAsync(Arg.Any<OrganizationIntegration>());
        sutProvider.GetDependency<ITeamsService>().Received(1).GetRedirectUrl(Arg.Any<string>(), expectedState);
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
        var fakeTime = new FakeTimeProvider(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        sutProvider.SetDependency<TimeProvider>(fakeTime, "timeProvider").Create();

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
        var expectedState = SetupProtect(sutProvider, integration, fakeTime);

        var response = await sutProvider.Sut.RedirectAsync(organizationId);

        Assert.Equal(expectedUrl, response.Url);
        sutProvider.GetDependency<ITeamsService>().Received(1).GetRedirectUrl(Arg.Any<string>(), expectedState);
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

        var response = await sutProvider.Sut.RedirectAsync(integration.OrganizationId);

        Assert.Equal("https://teams.example.com/authorize", response.Url);
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
    public async Task GetChannelsAsync_ChannelLookupFails_ThrowsBadRequest(
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
            .Returns((IReadOnlyList<TeamsChannel>?)null);

        await Assert.ThrowsAsync<BadRequestException>(
            async () => await sutProvider.Sut.GetChannelsAsync(integration.OrganizationId, integration.Id));
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

    /// <summary>
    /// Arranges a valid completion: the caller owns the org and the state matches an Initiated integration.
    /// </summary>
    private static OrganizationIntegrationOAuthCompleteRequestModel SetupComplete(
        SutProvider<TeamsIntegrationController> sutProvider,
        OrganizationIntegration integration)
    {
        integration.Type = IntegrationType.Teams;
        integration.Configuration = null;
        SetBaseServiceUriApi(sutProvider);
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.Sut.Url
            .RouteUrl(Arg.Is<UrlRouteContext>(c => c.RouteName == "TeamsIntegration_Create"))
            .Returns("/organizations/integrations/teams/create");
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(integration.OrganizationId)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integration.Id)
            .Returns(integration);
        var state = SetupState(sutProvider, integration);
        return new OrganizationIntegrationOAuthCompleteRequestModel { Code = _validTeamsCode, State = state };
    }

    private static async Task AssertNoSideEffects(SutProvider<TeamsIntegrationController> sutProvider)
    {
        await sutProvider.GetDependency<IIntegrationOAuthNonceStore>().DidNotReceiveWithAnyArgs()
            .TryConsumeAsync(default, default!);
        await sutProvider.GetDependency<ITeamsService>().DidNotReceiveWithAnyArgs()
            .ObtainTokenViaOAuth(default!, default!);
        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().DidNotReceiveWithAnyArgs()
            .UpsertAsync(default!);
    }

    private static string SetupState(SutProvider<TeamsIntegrationController> sutProvider, OrganizationIntegration integration) =>
        SetupState(sutProvider, new IntegrationOAuthStateTokenable(integration, _nonce, TimeProvider.System));

    private static string SetupState(
        SutProvider<TeamsIntegrationController> sutProvider,
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
        SutProvider<TeamsIntegrationController> sutProvider,
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

    private static void SetBaseServiceUriApi(SutProvider<TeamsIntegrationController> sutProvider)
    {
        sutProvider.GetDependency<IGlobalSettings>().BaseServiceUri.Api.Returns("https://api.example.com");
        sutProvider.GetDependency<IGlobalSettings>().BaseServiceUri.VaultWithHash.Returns("https://vault.example.com/#");
    }

    private static void AssertRedirectsToVault(IActionResult actionResult, Guid organizationId, string expectedResult)
    {
        var redirect = Assert.IsType<RedirectResult>(actionResult);
        Assert.Equal(
            $"https://vault.example.com/#/organizations/{organizationId}/integrations/event-management" +
            $"?integration=teams&result={expectedResult}",
            redirect.Url);
    }
}
