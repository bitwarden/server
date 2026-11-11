using Bitwarden.Server.Sdk.Environment;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;

namespace Bit.Notifications;

/// <summary>
/// Extends the Bitwarden SDK's bearer authentication with what the Notifications service needs on top:
/// the identity authority from <c>globalSettings</c>, and access tokens sent in the query string.
/// </summary>
/// <remarks>
/// Reading the identity authority from <c>globalSettings</c> is not specific to Notifications; every service
/// moving to the SDK's authentication will need it. This could be solved more generally later on, for example
/// in a GlobalSettings bridge library.
/// </remarks>
internal sealed class ConfigureJwtBearerOptions : IConfigureNamedOptions<JwtBearerOptions>
{
    private readonly IConfiguration _configuration;
    private readonly IBitwardenEnvironment _environment;
    private readonly IHostEnvironment _hostEnvironment;

    public ConfigureJwtBearerOptions(
        IConfiguration configuration,
        IBitwardenEnvironment environment,
        IHostEnvironment hostEnvironment)
    {
        _configuration = configuration;
        _environment = environment;
        _hostEnvironment = hostEnvironment;
    }

    public void Configure(string? name, JwtBearerOptions options)
    {
        if (name != JwtBearerDefaults.AuthenticationScheme)
        {
            return;
        }

        // An authority from Authentication:Schemes:Bearer wins, otherwise fall back to the same internal
        // identity URI the rest of the server uses.
        if (string.IsNullOrWhiteSpace(options.Authority))
        {
            var internalIdentity = GetInternalIdentity();
            if (!string.IsNullOrWhiteSpace(internalIdentity))
            {
                options.Authority = internalIdentity;
            }
            else if (_environment.SelfHosted)
            {
                options.Authority = "http://identity:5000";
            }
        }

        // Unlike the authority, this is always computed, so Authentication:Schemes:Bearer:RequireHttpsMetadata is
        // ignored. It matches what SharedWeb's identity authentication did.
        options.RequireHttpsMetadata = !_hostEnvironment.IsDevelopment()
            && (options.Authority?.StartsWith("https", StringComparison.OrdinalIgnoreCase) ?? false);

        // Browsers can't set headers on WebSocket and server-sent event requests, so SignalR clients send
        // the token as the access_token query parameter instead.
        options.Events ??= new JwtBearerEvents();
        options.Events.OnMessageReceived = context =>
        {
            if (string.IsNullOrEmpty(context.Request.Headers.Authorization)
                && context.Request.Query.TryGetValue("access_token", out var accessToken))
            {
                context.Token = accessToken;
            }

            return Task.CompletedTask;
        };
    }

    public void Configure(JwtBearerOptions options)
    {
        // Only the bearer scheme is configured.
    }

    /// <summary>
    /// Reads <c>globalSettings:baseServiceUri:internalIdentity</c>, honoring the self-hosted development override
    /// that <c>GlobalSettings</c> applies when <c>developSelfHosted</c> is set.
    /// </summary>
    private string? GetInternalIdentity()
    {
        if (_hostEnvironment.IsDevelopment() && _configuration.GetValue<bool>("developSelfHosted"))
        {
            var overrideValue = _configuration["Dev:SelfHostOverride:GlobalSettings:baseServiceUri:internalIdentity"];
            if (overrideValue is not null)
            {
                return overrideValue;
            }
        }

        return _configuration["globalSettings:baseServiceUri:internalIdentity"];
    }
}
