using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.Identity;
using Bit.Core.Auth.IdentityServer;
using Bit.Core.Billing.Services;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Duende.IdentityModel;
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

        // "Login with Bitwarden" partner clients receive identity claims only (sub/email/email_verified/name)
        // and never org/provider/premium/device claims, so a partner token exposes who the user is but nothing
        // about their vault, organizations, or entitlements. AddRequestedClaims filters to the scopes the
        // client was actually granted (openid/profile/email).
        if (context.Client.ClientId.StartsWith(OpenIdConnectPartner.ClientIdPrefix, StringComparison.Ordinal))
        {
            var partnerUser = await _userService.GetUserByPrincipalAsync(context.Subject);
            if (partnerUser != null)
            {
                var identityClaims = new List<Claim>
                {
                    // Pairwise subject: a per-partner identifier so two partners cannot correlate the same
                    // user by comparing `sub`, and the real user id is never exposed.
                    new(JwtClaimTypes.Subject, PairwiseSubject(partnerUser.Id, context.Client.ClientId)),
                    new(JwtClaimTypes.Email, partnerUser.Email ?? string.Empty),
                    new(JwtClaimTypes.EmailVerified, partnerUser.EmailVerified ? "true" : "false",
                        ClaimValueTypes.Boolean),
                };
                if (!string.IsNullOrWhiteSpace(partnerUser.Name))
                {
                    identityClaims.Add(new Claim(JwtClaimTypes.Name, partnerUser.Name));
                }
                context.AddRequestedClaims(identityClaims);
            }
            return;
        }

        // Whenever IdentityServer issues a new access token or services a UserInfo request, it calls
        // GetProfileDataAsync to determine which claims to include in the token or response.
        // In normal user identity scenarios, we have to look up the user to get their claims and update
        // the issued claims collection as claim info may have changed since the last time the user logged in or the
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

        // Determine which of the existingClaims to carry over onto the refreshed token.
        // Only known-safe user-identity claims are carried over; everything else (including membership and
        // authorization claims) is dropped so each refresh token reflects the user's current membership and
        // authorization state.
        var existingClaimsToKeep = existingClaims
            .Where(c =>
                // only allow user-identity claims to be carried over from the existing subject
                Claims.UserIdentityClaimTypes.Contains(c.Type) &&
                // keep the existing claim only when it is not being overwritten by a freshly built claim of the same type
                (newClaims.Count == 0 || !newClaims.Any(nc => nc.Type == c.Type))
            ).ToList();

        newClaims.AddRange(existingClaimsToKeep);
        if (newClaims.Count != 0)
        {
            context.IssuedClaims.AddRange(newClaims);
        }
    }

    /// <summary>
    /// Derives a stable, per-partner pseudonymous subject identifier for a user, so a token issued to one
    /// partner cannot be correlated with a token issued to another and the real user id is never exposed.
    /// </summary>
    private static string PairwiseSubject(Guid userId, string clientId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{userId:N}|{clientId}"));
        return CoreHelpers.Base64UrlEncode(hash);
    }

    public async Task IsActiveAsync(IsActiveContext context)
    {
        // Send Tokens are not refreshed so when the token has expired the user must request a new one via
        // the authentication method assigned to the send.
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
