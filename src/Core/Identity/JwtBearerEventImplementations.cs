using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.Authentication;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Identity;
using Bit.Core.Domains;

namespace Bit.Core.Identity
{
    public static class JwtBearerEventImplementations
    {
        public async static Task ValidatedTokenAsync(TokenValidatedContext context)
        {
            if(context.HttpContext.RequestServices == null)
            {
                throw new InvalidOperationException("RequestServices is null");
            }

            var userRepository = context.HttpContext.RequestServices.GetRequiredService<IUserRepository>();
            var userManager = context.HttpContext.RequestServices.GetRequiredService<UserManager<User>>();
            var signInManager = context.HttpContext.RequestServices.GetRequiredService<JwtBearerSignInManager>();

            var userId = userManager.GetUserId(context.Ticket.Principal);
            var user = await userRepository.GetByIdAsync(new Guid(userId));

            // validate security token
            if(!await signInManager.ValidateSecurityStampAsync(user, context.Ticket.Principal))
            {
                throw new SecurityTokenValidationException("Bad security stamp.");
            }

            // register the current context user
            var currentContext = context.HttpContext.RequestServices.GetRequiredService<CurrentContext>();
            currentContext.User = user;
        }

        public static Task AuthenticationFailedAsync(AuthenticationFailedContext context)
        {
            if(!context.HttpContext.User.Identity.IsAuthenticated)
            {
                context.State = EventResultState.HandledResponse;
                context.Ticket = new AuthenticationTicket(context.HttpContext.User, new AuthenticationProperties(), context.Options.AuthenticationScheme);
            }

            return Task.FromResult(0);
        }

        public static Task MessageReceivedAsync(MessageReceivedContext context)
        {
            if(!context.Request.Headers.ContainsKey("Authorization"))
            {
                context.Token = context.Request.Query["access_token"];
            }

            return Task.FromResult(0);
        }
    }
}
