using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Bit.Sso.Utilities
{
    public static class OpenIdConnectOptionsExtensions
    {
        public static async Task<bool> CouldHandleAsync(this OpenIdConnectOptions options, string scheme, HttpContext context)
        {
            // Determine this is a valid request for our handler
            if (options.CallbackPath != context.Request.Path &&
                options.RemoteSignOutPath != context.Request.Path &&
                options.SignedOutCallbackPath != context.Request.Path)
            {
                return false;
            }

            if (context.Request.Query["scheme"].FirstOrDefault() == scheme)
            {
                return true;
            }

            // Determine if the Authority matches the Referrer (short-cut)
            var referrer = context.Request.Headers["Referer"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(referrer) &&
                Uri.TryCreate(options.Authority, UriKind.Absolute, out var authorityUri) &&
                Uri.TryCreate(referrer, UriKind.Absolute, out var referrerUri) &&
                (referrerUri.IsBaseOf(authorityUri) || authorityUri.IsBaseOf(referrerUri)))
            {
                return true;
            }

            try
            {
                // Parse out the message
                OpenIdConnectMessage message = null;
                if (string.Equals(context.Request.Method, "GET", StringComparison.OrdinalIgnoreCase))
                {
                    message = new OpenIdConnectMessage(context.Request.Query.Select(pair => new KeyValuePair<string, string[]>(pair.Key, pair.Value)));
                }
                else if (string.Equals(context.Request.Method, "POST", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrEmpty(context.Request.ContentType) &&
                    context.Request.ContentType.StartsWith("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase) &&
                    context.Request.Body.CanRead)
                {
                    var form = await context.Request.ReadFormAsync();
                    message = new OpenIdConnectMessage(form.Select(pair => new KeyValuePair<string, string[]>(pair.Key, pair.Value)));
                }

                var state = message?.State;
                if (string.IsNullOrWhiteSpace(state))
                {
                    // State is required, it will fail later on for this reason.
                    return false;
                }

                // Handle State if we've gotten that back
                var decodedState = options.StateDataFormat.Unprotect(state);
                if (decodedState != null && decodedState.Items.ContainsKey("scheme"))
                {
                    return decodedState.Items["scheme"] == scheme;
                }
            }
            catch
            {
                return false;
            }

            // This is likely not an appropriate handler
            return false;
        }
    }
}
