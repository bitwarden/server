using Microsoft.AspNetCore.Http;
using System;
using System.Linq;

namespace Bit.Api.IdentityServer
{
    public static class TokenRetrieval
    {
        public static Func<HttpRequest, string> FromAuthorizationHeaderOrQueryString(string headerScheme = "Bearer",
            string qsName = "account_token")
        {
            return (request) =>
            {
                string authorization = request.Headers["Authorization"].FirstOrDefault();

                if(string.IsNullOrWhiteSpace(authorization))
                {
                    return request.Query[qsName].FirstOrDefault();
                }

                if(authorization.StartsWith(headerScheme + " ", StringComparison.OrdinalIgnoreCase))
                {
                    return authorization.Substring(headerScheme.Length + 1).Trim();
                }

                return null;
            };
        }
    }
}
