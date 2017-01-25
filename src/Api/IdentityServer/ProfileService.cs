using IdentityServer4.Services;
using System.Threading.Tasks;
using IdentityServer4.Models;
using Bit.Core.Repositories;
using Bit.Core.Services;
using System.Security.Claims;
using Bit.Core.Domains;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using System.Linq;
using Microsoft.Extensions.Options;
using System;

namespace Bit.Api.IdentityServer
{
    public class ProfileService : IProfileService
    {
        private readonly IUserService _userService;
        private readonly IUserRepository _userRepository;
        private IdentityOptions _identityOptions;

        public ProfileService(
            IUserRepository userRepository,
            IUserService userService,
            IOptions<IdentityOptions> identityOptionsAccessor)
        {
            _userRepository = userRepository;
            _userService = userService;
            _identityOptions = identityOptionsAccessor?.Value ?? new IdentityOptions();
        }

        public async Task GetProfileDataAsync(ProfileDataRequestContext context)
        {
            var claims = context.Subject.Claims.ToList();
            var user = await _userService.GetUserByPrincipalAsync(context.Subject);
            if(user != null)
            {
                claims.AddRange(new List<Claim>
                {
                    new Claim("plan", "0"), // free plan hard coded for now
                    new Claim("sstamp", user.SecurityStamp),
                    new Claim("email", user.Email),

                    // Deprecated claims for backwards compatability,
                    new Claim(_identityOptions.ClaimsIdentity.UserNameClaimType, user.Email),
                    new Claim(_identityOptions.ClaimsIdentity.SecurityStampClaimType, user.SecurityStamp)
                });

                if(!string.IsNullOrWhiteSpace(user.Name))
                {
                    claims.Add(new Claim("name", user.Name));
                }
            }

            if(claims.Count > 0)
            {
                context.AddFilteredClaims(claims);
            }
        }

        public async Task IsActiveAsync(IsActiveContext context)
        {
            var securityTokenClaim = context.Subject?.Claims.FirstOrDefault(c =>
                c.Type == _identityOptions.ClaimsIdentity.SecurityStampClaimType);
            var user = await _userService.GetUserByPrincipalAsync(context.Subject);

            if(user != null && securityTokenClaim != null)
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
}
