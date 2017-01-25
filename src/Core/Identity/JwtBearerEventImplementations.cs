using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.Authentication;
using Microsoft.IdentityModel.Tokens;
using Bit.Core.Services;

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

            var userService = context.HttpContext.RequestServices.GetRequiredService<IUserService>();
            var signInManager = context.HttpContext.RequestServices.GetRequiredService<JwtBearerSignInManager>();

            var userId = userService.GetProperUserId(context.Ticket.Principal);
            var user = await userService.GetUserByIdAsync(userId.Value);

            // validate security token
            if(!await signInManager.ValidateSecurityStampAsync(user, context.Ticket.Principal))
            {
                throw new SecurityTokenValidationException("Bad security stamp.");
            }
        }

        public static Task AuthenticationFailedAsync(AuthenticationFailedContext context)
        {
            if(!context.HttpContext.User.Identity.IsAuthenticated)
            {
                context.State = EventResultState.HandledResponse;
                context.Ticket = new AuthenticationTicket(context.HttpContext.User, new AuthenticationProperties(), 
                    context.Options.AuthenticationScheme);
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
