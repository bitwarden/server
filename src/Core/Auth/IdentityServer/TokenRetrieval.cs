﻿// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Microsoft.AspNetCore.Http;

namespace Bit.Core.Auth.IdentityServer;

public static class TokenRetrieval
{
    private static string _headerScheme = "Bearer ";
    private static string _queryScheme = "access_token";
    private static string _authHeader = "Authorization";

    public static Func<HttpRequest, string> FromAuthorizationHeaderOrQueryString()
    {
        return (request) =>
        {
            var authorization = request.Headers[_authHeader].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(authorization))
            {
                return request.Query[_queryScheme].FirstOrDefault();
            }

            if (authorization.StartsWith(_headerScheme, StringComparison.OrdinalIgnoreCase))
            {
                return authorization.Substring(_headerScheme.Length).Trim();
            }

            return null;
        };
    }
}
