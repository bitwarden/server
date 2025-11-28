using System.Security.Claims;
using Bit.Admin.Services;
using Bit.Core.Settings;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

public class CustomClaimsPrincipalFactory : UserClaimsPrincipalFactory<IdentityUser>
{
    private IAccessControlService _accessControlService;
    private readonly IGlobalSettings _globalSettings;

    public CustomClaimsPrincipalFactory(
        UserManager<IdentityUser> userManager,
        IOptions<IdentityOptions> optionsAccessor,
        IAccessControlService accessControlService,
        IGlobalSettings globalSettings)
            : base(userManager, optionsAccessor)
    {
        _accessControlService = accessControlService;
        _globalSettings = globalSettings;
    }

    public async override Task<ClaimsPrincipal> CreateAsync(IdentityUser user)
    {
        var principal = await base.CreateAsync(user);

        if (!_globalSettings.SelfHosted &&
            !string.IsNullOrEmpty(user.Email) &&
            principal.Identity != null)
        {
            var role = _accessControlService.GetUserRole(user.Email);

            if (!string.IsNullOrEmpty(role))
            {
                ((ClaimsIdentity)principal.Identity).AddClaims(
                new[] { new Claim(ClaimTypes.Role, role) });
            }
        }

        return principal;
    }
}
