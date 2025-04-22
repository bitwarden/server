using System.Security.Claims;
using System.Text.Encodings.Web;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Scim.Context;
using IdentityModel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Bit.Scim.Utilities;

public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationApiKeyRepository _organizationApiKeyRepository;
    private readonly IScimContext _scimContext;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock,
        IOrganizationRepository organizationRepository,
        IOrganizationApiKeyRepository organizationApiKeyRepository,
        IScimContext scimContext) :
        base(options, logger, encoder, clock)
    {
        _organizationRepository = organizationRepository;
        _organizationApiKeyRepository = organizationApiKeyRepository;
        _scimContext = scimContext;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var endpoint = Context.GetEndpoint();
        if (endpoint?.Metadata?.GetMetadata<IAllowAnonymous>() != null)
        {
            return AuthenticateResult.NoResult();
        }

        if (!_scimContext.OrganizationId.HasValue || _scimContext.Organization == null)
        {
            Logger.LogWarning("No organization.");
            return AuthenticateResult.Fail("Invalid parameters");
        }

        if (!Request.Headers.TryGetValue("Authorization", out var authHeader) || authHeader.Count != 1)
        {
            Logger.LogWarning("An API request was received without the Authorization header");
            return AuthenticateResult.Fail("Invalid parameters");
        }
        var apiKey = authHeader.ToString();
        if (apiKey.StartsWith("Bearer "))
        {
            apiKey = apiKey.Substring(7);
        }

        if (!_scimContext.Organization.Enabled || !_scimContext.Organization.UseScim ||
            _scimContext.ScimConfiguration == null || !_scimContext.ScimConfiguration.Enabled)
        {
            Logger.LogInformation("Org {organizationId} not able to use Scim.", _scimContext.OrganizationId);
            return AuthenticateResult.Fail("Invalid parameters");
        }

        var orgApiKey = (await _organizationApiKeyRepository
            .GetManyByOrganizationIdTypeAsync(_scimContext.Organization.Id, OrganizationApiKeyType.Scim))
            .FirstOrDefault();
        if (orgApiKey?.ApiKey != apiKey)
        {
            Logger.LogWarning("An API request was received with an invalid API key: {apiKey}", apiKey);
            return AuthenticateResult.Fail("Invalid parameters");
        }

        Logger.LogInformation("Org {organizationId} authenticated", _scimContext.OrganizationId);

        var claims = new[]
        {
            new Claim(JwtClaimTypes.ClientId, $"organization.{_scimContext.OrganizationId.Value}"),
            new Claim("client_sub", _scimContext.OrganizationId.Value.ToString()),
            new Claim(JwtClaimTypes.Scope, "api.scim"),
        };
        var identity = new ClaimsIdentity(claims, nameof(ApiKeyAuthenticationHandler));
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity),
            ApiKeyAuthenticationOptions.DefaultScheme);

        return AuthenticateResult.Success(ticket);
    }
}
