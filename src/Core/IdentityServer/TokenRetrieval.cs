using Microsoft.AspNetCore.Http;

namespace Bit.Core.IdentityServer;

public static class TokenRetrieval
{
    private static string _headerScheme = "Bearer ";
    private static string _queuryScheme = "access_token";
    private static string _authHeader = "Authorization";

    public static Func<HttpRequest, string> FromAuthorizationHeaderOrQueryString()
    {
        return (request) =>
        {
            var authorization = request.Headers[_authHeader].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(authorization))
            {
                return request.Query[_queuryScheme].FirstOrDefault();
            }

            if (authorization.StartsWith(_headerScheme, StringComparison.OrdinalIgnoreCase))
            {
                return authorization.Substring(_headerScheme.Length).Trim();
            }

            return null;
        };
    }
}
