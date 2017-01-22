using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Bit.Core.Identity
{
    public static class JwtBearerAppBuilderExtensions
    {
        public static IApplicationBuilder UseJwtBearerIdentity(this IApplicationBuilder app)
        {
            if(app == null)
            {
                throw new ArgumentNullException(nameof(app));
            }

            var marker = app.ApplicationServices.GetService<IdentityMarkerService>();
            if(marker == null)
            {
                throw new InvalidOperationException("Must Call AddJwtBearerIdentity");
            }

            var jwtOptions = app.ApplicationServices.GetRequiredService<IOptions<JwtBearerIdentityOptions>>().Value;
            var options = BuildJwtBearerOptions(jwtOptions);
            app.UseJwtBearerAuthentication(options);

            return app;
        }

        public static JwtBearerOptions BuildJwtBearerOptions(JwtBearerIdentityOptions jwtOptions)
        {
            var options = new JwtBearerOptions();

            // Basic settings - signing key to validate with, audience and issuer.
            options.TokenValidationParameters.IssuerSigningKey = jwtOptions.SigningCredentials.Key;
            options.TokenValidationParameters.ValidAudience = jwtOptions.Audience;
            options.TokenValidationParameters.ValidIssuer = jwtOptions.Issuer;

            options.TokenValidationParameters.RequireExpirationTime = true;
            options.TokenValidationParameters.RequireSignedTokens = false;

            // When receiving a token, check that we've signed it.
            options.TokenValidationParameters.RequireSignedTokens = false;

            //// When receiving a token, check that it is still valid.
            options.TokenValidationParameters.ValidateLifetime = true;

            // This defines the maximum allowable clock skew - i.e. provides a tolerance on the token expiry time 
            // when validating the lifetime. As we're creating the tokens locally and validating them on the same 
            // machines which should have synchronised time, this can be set to zero. Where external tokens are
            // used, some leeway here could be useful.
            options.TokenValidationParameters.ClockSkew = TimeSpan.FromMinutes(0);

            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = JwtBearerEventImplementations.ValidatedTokenAsync,
                OnAuthenticationFailed = JwtBearerEventImplementations.AuthenticationFailedAsync,
                OnMessageReceived = JwtBearerEventImplementations.MessageReceivedAsync
            };

            return options;
        }
    }
}
