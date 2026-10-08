using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Bit.AgentFill.Commands;
using Bit.AgentFill.Entities;
using Bit.AgentFill.Queries;
using Bit.Core.Auth.Identity;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bitwarden.Server.Sdk.Features;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Bit.AgentFill.Test;

public class AgentFillFeatureGateTests
{
    private class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "Test";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new ClaimsIdentity(SchemeName);
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }

    private readonly Guid _userId = Guid.NewGuid();
    private readonly IFeatureService _featureService = Substitute.For<IFeatureService>();
    private readonly ICreateApprovalRequestCommand _createCommand = Substitute.For<ICreateApprovalRequestCommand>();
    private readonly IAnswerApprovalRequestCommand _answerCommand = Substitute.For<IAnswerApprovalRequestCommand>();
    private readonly IGetApprovalRequestQuery _query = Substitute.For<IGetApprovalRequestQuery>();

    [Theory]
    [InlineData("POST", "")]
    [InlineData("GET", "/00000000-0000-0000-0000-000000000001")]
    [InlineData("PUT", "/00000000-0000-0000-0000-000000000001")]
    public async Task FlagOff_EveryEndpointIsNotFoundAndDoesNothing(string method, string path)
    {
        _featureService.IsEnabled(AgentFillFeatureFlags.AgentFillApprovals, Arg.Any<bool>()).Returns(false);
        using var host = await BuildHostAsync();

        var response = await host.GetTestClient().SendAsync(new HttpRequestMessage(new HttpMethod(method),
            $"/agent-fill/approvals{path}")
        {
            Content = JsonContent.Create(new { sealedRequest = "sealed", sealedResponse = "sealed" }),
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await _createCommand.DidNotReceiveWithAnyArgs().CreateAsync(default, default, default!);
        await _answerCommand.DidNotReceiveWithAnyArgs().AnswerAsync(default, default, default, default!);
        await _query.DidNotReceiveWithAnyArgs().GetAsync(default, default);
    }

    [Fact]
    public async Task FlagOn_PostReachesTheCommand()
    {
        _featureService.IsEnabled(AgentFillFeatureFlags.AgentFillApprovals, Arg.Any<bool>()).Returns(true);
        _createCommand.CreateAsync(default, default, default!).ReturnsForAnyArgs(new AgentFillApprovalRequest
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            SealedRequest = "sealed",
            CreationDate = DateTime.UtcNow,
            ExpirationDate = DateTime.UtcNow.AddMinutes(5),
        });
        using var host = await BuildHostAsync();

        var response = await host.GetTestClient().PostAsJsonAsync("/agent-fill/approvals",
            new { sealedRequest = "sealed" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await _createCommand.Received(1).CreateAsync(_userId, Arg.Any<Guid>(), "sealed");
    }

    private async Task<IHost> BuildHostAsync()
    {
        var currentContext = Substitute.For<ICurrentContext>();
        currentContext.UserId.Returns(_userId);
        currentContext.DeviceIdentifier.Returns("device-identifier");
        var deviceRepository = Substitute.For<IDeviceRepository>();
        deviceRepository.GetByIdentifierAsync("device-identifier", _userId)
            .Returns(new Device { Id = Guid.NewGuid(), UserId = _userId, Identifier = "device-identifier" });

        return await new HostBuilder()
            .ConfigureWebHost(webHost => webHost
                .UseTestServer()
                .ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddAuthentication(TestAuthenticationHandler.SchemeName)
                        .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                            TestAuthenticationHandler.SchemeName, null);
                    services.AddAuthorization(options =>
                        options.AddPolicy(Policies.Application, policy => policy.RequireAuthenticatedUser()));
                    services.Configure<FeatureCheckOptions>(options =>
                        options.OnFeatureCheckFailed = context =>
                            Results.NotFound().ExecuteAsync(context.HttpContext));
                    services.AddSingleton(_featureService);
                    services.AddSingleton(currentContext);
                    services.AddSingleton(deviceRepository);
                    services.AddSingleton(_createCommand);
                    services.AddSingleton(_answerCommand);
                    services.AddSingleton(_query);
                    services.AddSingleton(TimeProvider.System);
                })
                .Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseFeatureFlagChecks();
                    app.UseEndpoints(endpoints => endpoints.MapGroup("/agent-fill/approvals").MapAgentFillEndpoints());
                }))
            .StartAsync();
    }
}
