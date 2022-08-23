using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace Bit.IntegrationTestCommon;

public class FakeRemoteIpAddressMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IPAddress _fakeIpAddress;

    public FakeRemoteIpAddressMiddleware(RequestDelegate next, IPAddress fakeIpAddress = null)
    {
        _next = next;
        _fakeIpAddress = fakeIpAddress ?? IPAddress.Parse("127.0.0.1");
    }

    public async Task Invoke(HttpContext httpContext)
    {
        httpContext.Connection.RemoteIpAddress ??= _fakeIpAddress;
        await _next(httpContext);
    }
}

public class CustomStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            app.UseMiddleware<FakeRemoteIpAddressMiddleware>();
            next(app);
        };
    }
}
