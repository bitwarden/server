using Microsoft.AspNetCore.Http;
using System;
using System.Linq;

namespace Bit.Api.Utilities
{
    public static class TokenRetrieval
    {
        public static Func<HttpRequest, string> FromAuthorizationHeaderOrQueryString(string headerScheme = "Bearer",
            string qsName = "access_token")
        {
            return (request) =>
            {
                var authorization = request.Headers["Authorization"].FirstOrDefault();

                if(string.IsNullOrWhiteSpace(authorization))
                {
                    // Bearer token could exist in the 'Content-Language' header on clients that want to avoid pre-flights.
                    var languageAuth = request.Headers["Content-Language"].FirstOrDefault();
                    if(string.IsNullOrWhiteSpace(languageAuth) ||
                        !languageAuth.StartsWith($"{headerScheme} ", StringComparison.OrdinalIgnoreCase))
                    {
                        return request.Query[qsName].FirstOrDefault();
                    }
                    else
                    {
                        authorization = languageAuth.Split(',')[0];
                    }
                }

                if(authorization.StartsWith($"{headerScheme} ", StringComparison.OrdinalIgnoreCase))
                {
                    return authorization.Substring(headerScheme.Length + 1).Trim();
                }

                return null;
            };
        }
    }
}
