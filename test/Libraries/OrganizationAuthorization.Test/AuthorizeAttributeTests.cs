using System.Security.Claims;
using System.Text.Encodings.Web;
using Bit.OrganizationAuthorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Authorization;

public class AuthorizeAttributeTests
{
    private class AllowedRequirement : IAuthorizationRequirement;

    private class AllowedRequirementHandler : AuthorizationHandler<AllowedRequirement>
    {
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, AllowedRequirement requirement)
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }
    }

    // Deliberately has no registered handler, so ASP.NET Core's default fail-closed behavior applies.
    private class UnhandledRequirement : IAuthorizationRequirement;

    // AuthorizeAttribute<T> combines with the default policy, which requires an authenticated user,
    // so this needs to actually authenticate the request rather than no-op it.
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

    private static async Task<IHost> BuildHostAsync() =>
        await new HostBuilder()
            .ConfigureWebHost(webHost => webHost
                .UseTestServer()
                .ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddAuthorization();
                    services.AddAuthentication(TestAuthenticationHandler.SchemeName)
                        .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                            TestAuthenticationHandler.SchemeName, null);
                    services.AddSingleton<IAuthorizationHandler, AllowedRequirementHandler>();
                })
                .Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGet("/allowed", () => "ok")
                            .RequireAuthorization(new AuthorizeAttribute<AllowedRequirement>());

                        endpoints.MapGet("/unhandled", () => "ok")
                            .RequireAuthorization(new AuthorizeAttribute<UnhandledRequirement>());
                    });
                }))
            .StartAsync();

    [Fact]
    public async Task RequireAuthorization_GivenSatisfiedRequirement_AllowsRequest()
    {
        using var host = await BuildHostAsync();
        using var client = host.GetTestClient();

        var response = await client.GetAsync("/allowed");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RequireAuthorization_GivenUnsatisfiedRequirement_ForbidsRequest()
    {
        using var host = await BuildHostAsync();
        using var client = host.GetTestClient();

        var response = await client.GetAsync("/unhandled");

        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
    }
}
