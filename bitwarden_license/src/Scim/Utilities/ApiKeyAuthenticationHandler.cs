using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using IdentityModel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bit.Scim.Utilities
{
    public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
    {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IOrganizationApiKeyRepository _organizationApiKeyRepository;

        public ApiKeyAuthenticationHandler(
            IOptionsMonitor<ApiKeyAuthenticationOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            ISystemClock clock,
            IOrganizationRepository organizationRepository,
            IOrganizationApiKeyRepository organizationApiKeyRepository) :
            base(options, logger, encoder, clock)
        {
            _organizationRepository = organizationRepository;
            _organizationApiKeyRepository = organizationApiKeyRepository;
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            string orgIdString = null;
            if (Request.RouteValues.TryGetValue("organizationId", out var orgIdObject))
            {
                orgIdString = orgIdObject?.ToString();
            }

            if (string.IsNullOrWhiteSpace(orgIdString) || !Guid.TryParse(orgIdString, out var orgId))
            {
                Logger.LogWarning("Could not load org id from route.");
                return AuthenticateResult.Fail("Invalid parameters");
            }

            if (!Request.Headers.TryGetValue("Authorization", out var authHeader) || authHeader.Count != 1)
            {
                Logger.LogWarning("An API request was received without the Authorization header");
                return AuthenticateResult.Fail("Invalid parameters");
            }
            var apiKey = authHeader.ToString();

            var org = await _organizationRepository.GetByIdAsync(orgId);
            var orgApiKey = (await _organizationApiKeyRepository
                // TODO: Change to Scim type?
                .GetManyByOrganizationIdTypeAsync(org.Id, OrganizationApiKeyType.Default))
                .FirstOrDefault();
            if (org == null || !org.Enabled || !org.UseDirectory || orgApiKey?.ApiKey != apiKey)
            {
                Logger.LogWarning($"An API request was received with an invalid API key: {apiKey}");
                return AuthenticateResult.Fail("Invalid parameters");
            }

            Logger.LogInformation("Org authenticated");

            var claims = new[]
            {
                new Claim(JwtClaimTypes.ClientId, $"organization.{orgId}"),
                new Claim("client_sub", orgId.ToString()),
                new Claim(JwtClaimTypes.Scope, "api.scim"),
            };
            var identity = new ClaimsIdentity(claims, nameof(ApiKeyAuthenticationHandler));
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity),
                ApiKeyAuthenticationOptions.DefaultScheme);

            return AuthenticateResult.Success(ticket);
        }
    }
}
