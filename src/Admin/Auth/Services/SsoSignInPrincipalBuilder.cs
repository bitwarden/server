// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.Security.Claims;
using Bit.Admin.Auth.IdentityServer;

namespace Bit.Admin.Auth.Services;

// Assembles the ClaimsPrincipal that gets written to the Admin Portal SSO cookie on sign-in.
// The caller hands over the verified external principal and the email, and gets back a
// principal ready for HttpContext.SignInAsync.
//
// Deliberately bypasses SignInManager / UserManager so the SSO path does not need a backing
// user store entry and does not stamp a SecurityStamp claim. The companion to that choice is
// that the controller signs the result into the dedicated AdminAuthenticationSchemes.SsoCookie
// scheme (not Identity's application cookie); the Identity cookie's SecurityStampValidator
// would otherwise reject this principal within 30 seconds because no matching user exists in
// the store.
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
        // Match the scheme the controller signs this principal into so middleware traces and
        // any code keying off Identity.AuthenticationType report the actual scheme.
        var identity = new ClaimsIdentity(AdminAuthenticationSchemes.SsoCookie);

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
