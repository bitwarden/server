using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.OrganizationConnectionConfigs;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Scim.Context;
using Bit.Scim.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Duende.IdentityModel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Bit.Scim.Test.Utilities;

[SutProviderCustomize]
public class ApiKeyAuthenticationHandlerTests
{
    [Theory]
    [BitAutoData]
    public async Task HandleAuthenticate_ValidApiKey_Succeeds(SutProvider<ApiKeyAuthenticationHandler> sutProvider, Organization organization, string apiKey)
    {
        ArrangeScimEnabledOrganization(sutProvider, organization, apiKey);

        var result = await AuthenticateAsync(sutProvider, $"Bearer {apiKey}");

        Assert.True(result.Succeeded);
        Assert.Equal($"organization.{organization.Id}",
            result.Principal.FindFirst(JwtClaimTypes.ClientId)?.Value);
        Assert.Equal(organization.Id.ToString(), result.Principal.FindFirst("client_sub")?.Value);
        Assert.Equal("api.scim", result.Principal.FindFirst(JwtClaimTypes.Scope)?.Value);
    }

    [Theory]
    [BitAutoData]
    public async Task HandleAuthenticate_InvalidApiKey_FailsWithoutLoggingSubmittedKey(
        SutProvider<ApiKeyAuthenticationHandler> sutProvider, Organization organization, string apiKey)
    {
        var logger = ArrangeScimEnabledOrganization(sutProvider, organization, apiKey);
        var submittedKey = apiKey + "-wrong";

        var result = await AuthenticateAsync(sutProvider, $"Bearer {submittedKey}");

        Assert.False(result.Succeeded);
        logger.DidNotReceive().Log(
            Arg.Any<LogLevel>(),
            Arg.Any<EventId>(),
            Arg.Is<object>(state => state.ToString().Contains(submittedKey)),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception, string>>());
    }

    [Theory]
    [BitAutoData]
    public async Task HandleAuthenticate_NoMatchingApiKey_Fails(
        SutProvider<ApiKeyAuthenticationHandler> sutProvider, Organization organization, string apiKey)
    {
        ArrangeScimEnabledOrganization(sutProvider, organization, apiKey);
        sutProvider.GetDependency<IOrganizationApiKeyRepository>()
            .GetManyByOrganizationIdTypeAsync(organization.Id, OrganizationApiKeyType.Scim)
            .Returns([]);

        var result = await AuthenticateAsync(sutProvider, $"Bearer {apiKey}");

        Assert.False(result.Succeeded);
    }

    [Theory]
    [BitAutoData]
    public async Task HandleAuthenticate_NoOrganization_Fails(SutProvider<ApiKeyAuthenticationHandler> sutProvider)
    {
        ArrangeLogger(sutProvider);

        sutProvider.GetDependency<IScimContext>().OrganizationId.Returns((Guid?)null);
        sutProvider.GetDependency<IScimContext>().Organization.Returns((Organization)null);

        var result = await AuthenticateAsync(sutProvider, "Bearer anything");

        Assert.False(result.Succeeded);
    }

    [Theory]
    [BitAutoData]
    public async Task HandleAuthenticate_MissingAuthorizationHeader_Fails(SutProvider<ApiKeyAuthenticationHandler> sutProvider, Organization organization, string apiKey)
    {
        ArrangeScimEnabledOrganization(sutProvider, organization, apiKey);

        var result = await AuthenticateAsync(sutProvider, authorizationHeader: null);

        Assert.False(result.Succeeded);
    }

    [Theory]
    [BitAutoData]
    public async Task HandleAuthenticate_OrganizationCannotUseScim_Fails(SutProvider<ApiKeyAuthenticationHandler> sutProvider, Organization organization, string apiKey)
    {
        organization.Enabled = true;
        organization.UseScim = false;

        ArrangeLogger(sutProvider);

        var scimContext = sutProvider.GetDependency<IScimContext>();
        scimContext.OrganizationId.Returns(organization.Id);
        scimContext.Organization.Returns(organization);
        scimContext.ScimConfiguration.Returns(new ScimConfig { Enabled = true });

        var result = await AuthenticateAsync(sutProvider, $"Bearer {apiKey}");

        Assert.False(result.Succeeded);
    }

    private static ILogger ArrangeLogger(SutProvider<ApiKeyAuthenticationHandler> sutProvider)
    {
        var logger = Substitute.For<ILogger>();
        sutProvider.GetDependency<ILoggerFactory>().CreateLogger(Arg.Any<string>()).Returns(logger);
        return logger;
    }

    private static ILogger ArrangeScimEnabledOrganization(SutProvider<ApiKeyAuthenticationHandler> sutProvider, Organization organization, string apiKey)
    {
        var logger = ArrangeLogger(sutProvider);

        organization.Enabled = true;
        organization.UseScim = true;

        var scimContext = sutProvider.GetDependency<IScimContext>();
        scimContext.OrganizationId.Returns(organization.Id);
        scimContext.Organization.Returns(organization);
        scimContext.ScimConfiguration.Returns(new ScimConfig { Enabled = true });

        sutProvider.GetDependency<IOrganizationApiKeyRepository>()
            .GetManyByOrganizationIdTypeAsync(organization.Id, OrganizationApiKeyType.Scim)
            .Returns([new OrganizationApiKey { OrganizationId = organization.Id, Type = OrganizationApiKeyType.Scim, ApiKey = apiKey }
            ]);

        return logger;
    }

    private static async Task<AuthenticateResult> AuthenticateAsync(SutProvider<ApiKeyAuthenticationHandler> sutProvider, string? authorizationHeader)
    {
        sutProvider.GetDependency<IOptionsMonitor<ApiKeyAuthenticationOptions>>()
            .Get(Arg.Any<string>())
            .Returns(new ApiKeyAuthenticationOptions());

        var httpContext = new DefaultHttpContext();
        if (authorizationHeader is not null)
        {
            httpContext.Request.Headers.Authorization = authorizationHeader;
        }

        var scheme = new AuthenticationScheme(
            ApiKeyAuthenticationOptions.DefaultScheme, null, typeof(ApiKeyAuthenticationHandler));
        await sutProvider.Sut.InitializeAsync(scheme, httpContext);

        return await sutProvider.Sut.AuthenticateAsync();
    }
}
