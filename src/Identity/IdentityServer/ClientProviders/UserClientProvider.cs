#nullable enable

using System.Collections.ObjectModel;
using System.Security.Claims;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Services;
using Bit.Core.Context;
using Bit.Core.Identity;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Duende.IdentityServer.Models;
using IdentityModel;

namespace Bit.Identity.IdentityServer.ClientProviders;

public class UserClientProvider : IClientProvider
{
    private readonly IUserRepository _userRepository;
    private readonly ICurrentContext _currentContext;
    private readonly ILicensingService _licensingService;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IProviderUserRepository _providerUserRepository;

    public UserClientProvider(
        IUserRepository userRepository,
        ICurrentContext currentContext,
        ILicensingService licensingService,
        IOrganizationUserRepository organizationUserRepository,
        IProviderUserRepository providerUserRepository)
    {
        _userRepository = userRepository;
        _currentContext = currentContext;
        _licensingService = licensingService;
        _organizationUserRepository = organizationUserRepository;
        _providerUserRepository = providerUserRepository;
    }

    public async Task<Client?> GetAsync(string identifier)
    {
        if (!Guid.TryParse(identifier, out var userId))
        {
            return null;
        }

        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            return null;
        }

        var claims = new Collection<ClientClaim>
        {
            new(JwtClaimTypes.Subject, user.Id.ToString()),
            new(JwtClaimTypes.AuthenticationMethod, "Application", "external"),
            new(Claims.Type, IdentityClientType.User.ToString()),
        };
        var orgs = await _currentContext.OrganizationMembershipAsync(_organizationUserRepository, user.Id);
        var providers = await _currentContext.ProviderMembershipAsync(_providerUserRepository, user.Id);
        var isPremium = await _licensingService.ValidateUserPremiumAsync(user);
        foreach (var claim in CoreHelpers.BuildIdentityClaims(user, orgs, providers, isPremium))
        {
            var upperValue = claim.Value.ToUpperInvariant();
            var isBool = upperValue is "TRUE" or "FALSE";
            claims.Add(isBool
                ? new ClientClaim(claim.Key, claim.Value, ClaimValueTypes.Boolean)
                : new ClientClaim(claim.Key, claim.Value)
            );
        }

        return new Client
        {
            ClientId = $"user.{userId}",
            RequireClientSecret = true,
            ClientSecrets = { new Secret(user.ApiKey.Sha256()) },
            AllowedScopes = new[] { "api" },
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            AccessTokenLifetime = 3600 * 1,
            ClientClaimsPrefix = null,
            Claims = claims,
        };
    }
}
