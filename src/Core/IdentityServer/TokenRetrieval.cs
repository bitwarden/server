using Microsoft.AspNetCore.Http;
using System;
using System.Linq;

namespace Bit.Core.IdentityServer
{
    public static class TokenRetrieval
    {
        public static Func<HttpRequest, string> FromAuthorizationHeaderOrQueryString(string[] authHeaderSchemes)
        {
            return (request) =>
            {
                var authorization = request.Headers["Authorization"].FirstOrDefault();

                if(string.IsNullOrWhiteSpace(authorization))
                {
                    // Bearer token could exist in the 'Content-Language' header on clients that want to avoid pre-flights.
                    var languageAuth = request.Headers["Content-Language"].FirstOrDefault();
                    if(string.IsNullOrWhiteSpace(languageAuth) ||
                        !languageAuth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    {
                        return request.Query["access_token"].FirstOrDefault();
                    }
                    else
                    {
                        authorization = languageAuth.Split(',')[0];
                    }
                }

                foreach(var headerScheme in authHeaderSchemes)
                {
                    if(authorization.StartsWith($"{headerScheme} ", StringComparison.OrdinalIgnoreCase))
                    {
                        return authorization.Substring(headerScheme.Length + 1).Trim();
                    }
                }

                return null;
            };
        }
    }
}
