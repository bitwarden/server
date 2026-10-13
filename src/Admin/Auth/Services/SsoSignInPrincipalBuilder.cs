// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.Security.Claims;
using Microsoft.AspNetCore.Identity;

namespace Bit.Admin.Auth.Services;

// Assembles the ClaimsPrincipal that gets written to the Admin Portal application cookie on
// SSO sign-in. The caller hands over the verified external principal and the email, and gets back a
// principal ready for HttpContext.SignInAsync.
//
// Deliberately bypasses SignInManager / UserManager so the SSO path does not need a backing
// user store entry and does not stamp a SecurityStamp claim. Without the stamp claim, the
// SecurityStampValidator skips its periodic re-fetch, which is what we want for a session
// whose source of truth is the IdP, not local config.
public interface ISsoSignInPrincipalBuilder
{
    ClaimsPrincipal Build(string email, ClaimsPrincipal externalPrincipal);
}

public class SsoSignInPrincipalBuilder : ISsoSignInPrincipalBuilder
{
    private readonly IGroupClaimRoleMapper _groupClaimRoleMapper;

    public SsoSignInPrincipalBuilder(IGroupClaimRoleMapper groupClaimRoleMapper)
    {
        _groupClaimRoleMapper = groupClaimRoleMapper;
    }

    public ClaimsPrincipal Build(string email, ClaimsPrincipal externalPrincipal)
    {
        // AuthenticationType is required for ClaimsIdentity.IsAuthenticated to return true.
        // Using the application scheme name is conventional and makes the identity recognizable
        // in middleware traces.
        var identity = new ClaimsIdentity(IdentityConstants.ApplicationScheme);

        identity.AddClaim(new Claim(ClaimTypes.Name, email));
        identity.AddClaim(new Claim(ClaimTypes.Email, email));
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, email));

        foreach (var role in _groupClaimRoleMapper.MapRoles(externalPrincipal))
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
        }

        return new ClaimsPrincipal(identity);
    }
}
