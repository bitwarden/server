using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNet.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using System.IdentityModel.Tokens;
using Bit.Core.Repositories;
using Microsoft.AspNet.Authentication;
using Microsoft.AspNet.Http.Authentication;

namespace Bit.Core.Identity
{
    public static class JwtBearerEventImplementations
    {
        public async static Task ValidatedTokenAsync(ValidatedTokenContext context)
        {
            if(context.HttpContext.RequestServices == null)
            {
                throw new InvalidOperationException("RequestServices is null");
            }

            var userRepository = context.HttpContext.RequestServices.GetRequiredService<IUserRepository>();
            var manager = context.HttpContext.RequestServices.GetRequiredService<JwtBearerSignInManager>();

            var userId = context.AuthenticationTicket.Principal.GetUserId();
            var user = await userRepository.GetByIdAsync(userId);

            // validate security token
            if(!await manager.ValidateSecurityStampAsync(user, context.AuthenticationTicket.Principal))
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
                context.AuthenticationTicket = new AuthenticationTicket(context.HttpContext.User, new AuthenticationProperties(), context.Options.AuthenticationScheme);
            }

            return Task.FromResult(0);
        }
    }
}
