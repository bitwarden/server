﻿// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.Net;
using Bit.IntegrationTestCommon.Factories;
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
        _fakeIpAddress = fakeIpAddress ?? IPAddress.Parse(FactoryConstants.WhitelistedIp);
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
