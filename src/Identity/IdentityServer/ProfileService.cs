using System.Security.Claims;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Services;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Identity;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;

namespace Bit.Identity.IdentityServer;

public class ProfileService : IProfileService
{
    private readonly IUserService _userService;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IProviderUserRepository _providerUserRepository;
    private readonly IProviderOrganizationRepository _providerOrganizationRepository;
    private readonly ILicensingService _licensingService;
    private readonly ICurrentContext _currentContext;

    public ProfileService(
        IUserService userService,
        IOrganizationUserRepository organizationUserRepository,
        IProviderUserRepository providerUserRepository,
        IProviderOrganizationRepository providerOrganizationRepository,
        ILicensingService licensingService,
        ICurrentContext currentContext)
    {
        _userService = userService;
        _organizationUserRepository = organizationUserRepository;
        _providerUserRepository = providerUserRepository;
        _providerOrganizationRepository = providerOrganizationRepository;
        _licensingService = licensingService;
        _currentContext = currentContext;
    }

    public async Task GetProfileDataAsync(ProfileDataRequestContext context)
    {
        var existingClaims = context.Subject.Claims;

        // If the client is a Send client, we do not add any additional claims
        if (context.Client.ClientId == BitwardenClient.Send)
        {
            // preserve all claims that were already on context.Subject
            // which includes the ones added by the SendAccessGrantValidator
            context.IssuedClaims.AddRange(existingClaims);
            return;
        }

        // Whenever IdentityServer issues a new access token or services a UserInfo request, it calls
        // GetProfileDataAsync to determine which claims to include in the token or response.
        // In normal user identity scenarios, we have to look up the user to get their claims and update
        // the issued claims collection as claim info can have changed since the last time the user logged in or the
        // last time the token was issued.
        var newClaims = new List<Claim>();
        var user = await _userService.GetUserByPrincipalAsync(context.Subject);
        if (user != null)
        {
            var isPremium = await _licensingService.ValidateUserPremiumAsync(user);
            var orgs = await _currentContext.OrganizationMembershipAsync(_organizationUserRepository, user.Id);
            var providers = await _currentContext.ProviderMembershipAsync(_providerUserRepository, user.Id);
            foreach (var claim in CoreHelpers.BuildIdentityClaims(user, orgs, providers, isPremium))
            {
                var upperValue = claim.Value.ToUpperInvariant();
                var isBool = upperValue == "TRUE" || upperValue == "FALSE";
                newClaims.Add(isBool ?
                    new Claim(claim.Key, claim.Value, ClaimValueTypes.Boolean) :
                    new Claim(claim.Key, claim.Value)
                );
            }
        }

        // filter out any of the new claims
        var existingClaimsToKeep = existingClaims
            .Where(c =>
                // Drop any org claims
                !c.Type.StartsWith("org") &&
                // If we have no new claims, then keep the existing claims
                // If we have new claims, then keep the existing claim if it does not match a new claim type
                (newClaims.Count == 0 || !newClaims.Any(nc => nc.Type == c.Type))
            ).ToList();

        newClaims.AddRange(existingClaimsToKeep);
        if (newClaims.Count != 0)
        {
            context.IssuedClaims.AddRange(newClaims);
        }
    }

    public async Task IsActiveAsync(IsActiveContext context)
    {
        if (context.Client.ClientId == BitwardenClient.Send)
        {
            context.IsActive = true;
            return;
        }

        // We add the security stamp claim to the persisted grant when we issue the refresh token.
        // IdentityServer will add this claim to the subject, and here we evaluate whether the security stamp that
        // was persisted matches the current security stamp of the user. If it does not match, then the user has performed
        // an operation that we want to invalidate the refresh token.
        var securityTokenClaim = context.Subject?.Claims.FirstOrDefault(c => c.Type == Claims.SecurityStamp);
        var user = await _userService.GetUserByPrincipalAsync(context.Subject);

        if (user != null && securityTokenClaim != null)
        {
            context.IsActive = string.Equals(user.SecurityStamp, securityTokenClaim.Value,
                StringComparison.InvariantCultureIgnoreCase);
            return;
        }
        else
        {
            context.IsActive = true;
        }
    }
}
