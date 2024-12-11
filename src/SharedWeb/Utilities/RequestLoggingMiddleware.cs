using System.Collections;
using Bit.Core;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Bit.SharedWeb.Utilities;

public sealed class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;
    private readonly GlobalSettings _globalSettings;

    public RequestLoggingMiddleware(
        RequestDelegate next,
        ILogger<RequestLoggingMiddleware> logger,
        GlobalSettings globalSettings
    )
    {
        _next = next;
        _logger = logger;
        _globalSettings = globalSettings;
    }

    public Task Invoke(HttpContext context, IFeatureService featureService)
    {
        if (!featureService.IsEnabled(FeatureFlagKeys.RemoveServerVersionHeader))
        {
            context.Response.OnStarting(() =>
            {
                context.Response.Headers.Append("Server-Version", AssemblyHelpers.GetVersion());
                return Task.CompletedTask;
            });
        }

        using (
            _logger.BeginScope(
                new RequestLogScope(
                    context.GetIpAddress(_globalSettings),
                    GetHeaderValue(context, "user-agent"),
                    GetHeaderValue(context, "device-type"),
                    GetHeaderValue(context, "device-type"),
                    GetHeaderValue(context, "bitwarden-client-version")
                )
            )
        )
        {
            return _next(context);
        }

        static string? GetHeaderValue(HttpContext httpContext, string header)
        {
            if (httpContext.Request.Headers.TryGetValue(header, out var value))
            {
                return value;
            }

            return null;
        }
    }

    private sealed class RequestLogScope : IReadOnlyList<KeyValuePair<string, object?>>
    {
        private string? _cachedToString;

        public RequestLogScope(
            string? ipAddress,
            string? userAgent,
            string? deviceType,
            string? origin,
            string? clientVersion
        )
        {
            IpAddress = ipAddress;
            UserAgent = userAgent;
            DeviceType = deviceType;
            Origin = origin;
            ClientVersion = clientVersion;
        }

        public KeyValuePair<string, object?> this[int index]
        {
            get
            {
                if (index == 0)
                {
                    return new KeyValuePair<string, object?>(nameof(IpAddress), IpAddress);
                }
                else if (index == 1)
                {
                    return new KeyValuePair<string, object?>(nameof(UserAgent), UserAgent);
                }
                else if (index == 2)
                {
                    return new KeyValuePair<string, object?>(nameof(DeviceType), DeviceType);
                }
                else if (index == 3)
                {
                    return new KeyValuePair<string, object?>(nameof(Origin), Origin);
                }
                else if (index == 4)
                {
                    return new KeyValuePair<string, object?>(nameof(ClientVersion), ClientVersion);
                }

                throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        public int Count => 5;

        public string? IpAddress { get; }
        public string? UserAgent { get; }
        public string? DeviceType { get; }
        public string? Origin { get; }
        public string? ClientVersion { get; }

        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
        {
            for (var i = 0; i < Count; i++)
            {
                yield return this[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public override string ToString()
        {
            _cachedToString ??=
                $"IpAddress:{IpAddress} UserAgent:{UserAgent} DeviceType:{DeviceType} Origin:{Origin} ClientVersion:{ClientVersion}";
            return _cachedToString;
        }
    }
}
