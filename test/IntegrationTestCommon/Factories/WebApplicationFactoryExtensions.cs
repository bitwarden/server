using System.Net;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Primitives;

namespace Bit.IntegrationTestCommon.Factories;

public static class WebApplicationFactoryExtensions
{
    private static async Task<HttpContext> SendAsync(this TestServer server,
        HttpMethod method,
        string requestUri,
        HttpContent content = null,
        Action<HttpContext> extraConfiguration = null)
    {
        return await server.SendAsync(httpContext =>
        {
            // Automatically set the whitelisted IP so normal tests do not run into rate limit issues
            // to test rate limiter, use the extraConfiguration parameter to set Connection.RemoteIpAddress
            // it runs after this so it will take precedence.
            httpContext.Connection.RemoteIpAddress = IPAddress.Parse(FactoryConstants.WhitelistedIp);

            httpContext.Request.Path = new PathString(requestUri);
            httpContext.Request.Method = method.Method;

            if (content != null)
            {
                foreach (var header in content.Headers)
                {
                    httpContext.Request.Headers.Add(header.Key, new StringValues(header.Value.ToArray()));
                }

                httpContext.Request.Body = content.ReadAsStream();
            }

            extraConfiguration?.Invoke(httpContext);
        });
    }
    public static Task<HttpContext> PostAsync(this TestServer server,
        string requestUri,
        HttpContent content,
        Action<HttpContext> extraConfiguration = null)
        => SendAsync(server, HttpMethod.Post, requestUri, content, extraConfiguration);
    public static Task<HttpContext> GetAsync(this TestServer server,
        string requestUri,
        Action<HttpContext> extraConfiguration = null)
        => SendAsync(server, HttpMethod.Get, requestUri, content: null, extraConfiguration);
    public static Task<HttpContext> PutAsync(this TestServer server,
        string requestUri,
        HttpContent content,
        Action<HttpContext> extraConfiguration = null)
        => SendAsync(server, HttpMethod.Put, requestUri, content, extraConfiguration);
    public static Task<HttpContext> PatchAsync(this TestServer server,
        string requestUri,
        HttpContent content,
        Action<HttpContext> extraConfiguration = null)
        => SendAsync(server, HttpMethod.Patch, requestUri, content, extraConfiguration);
    public static Task<HttpContext> DeleteAsync(this TestServer server,
        string requestUri,
        HttpContent content,
        Action<HttpContext> extraConfiguration = null)
        => SendAsync(server, HttpMethod.Delete, requestUri, content: content, extraConfiguration);

    public static HttpContext SetAuthEmail(this HttpContext context, string username)
    {
        context.Request.Headers.Add("Auth-Email", CoreHelpers.Base64UrlEncodeString(username));
        return context;
    }

    public static HttpContext SetIp(this HttpContext context, string ip)
    {
        context.Connection.RemoteIpAddress = IPAddress.Parse(ip);
        return context;
    }

    public static async Task<string> ReadBodyAsStringAsync(this HttpContext context)
    {
        using var sr = new StreamReader(context.Response.Body);
        return await sr.ReadToEndAsync();
    }
}
