using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Primitives;

namespace Bit.IntegrationTestCommon.Factories
{
    public static class WebApplicationFactoryExtensions
    {
        public static async Task<HttpContext> SendAsync(this TestServer server,
            HttpMethod method,
            string requestUri,
            HttpContent content = null,
            Action<HttpContext> extraConfiguration = null)
        {
            return await server.SendAsync(httpContext =>
            {
                httpContext.Connection.RemoteIpAddress = IPAddress.Parse("1.1.1.1");

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
        public static async Task<string> ReadBodyAsStringAsync(this HttpContext context)
        {
            using var sr = new StreamReader(context.Response.Body);
            return await sr.ReadToEndAsync();
        }
    }
}
