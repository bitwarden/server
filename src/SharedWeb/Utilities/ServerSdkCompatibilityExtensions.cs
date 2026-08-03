using Bit.Core;
using Bit.Core.Context;
using Bit.Core.Models.Api;
using Bit.Core.Services.Implementations;
using Bit.Core.Settings;
using Bitwarden.Server.Sdk.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Bit.SharedWeb.Utilities;

public static class ServerSdkCompatibilityExtensions
{
    /// <summary>
    /// Wires the server up to the <c>Bitwarden.Server.Sdk</c> feature flag pipeline: maps the
    /// existing <see cref="GlobalSettings.LaunchDarkly"/> settings onto <see cref="FeatureFlagOptions"/>,
    /// registers a server-aware <see cref="IContextBuilder"/>, and registers the deprecated
    /// <see cref="Bit.Core.Services.IFeatureService"/> as a shim that delegates to the new SDK service.
    /// </summary>
    /// <remarks>
    /// Assumes the SDK's own feature flag services have already been registered (either through
    /// <c>BitIncludeFeatures</c> auto-wireup or an explicit <c>AddFeatureFlagServices()</c> call).
    /// </remarks>
    public static IServiceCollection ApplyServerCompatibilityLayer(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddGlobalSettings();

        services.Configure<FeatureCheckOptions>(options =>
        {
            // OnFeatureCheckFailed fires from middleware before MVC routing, so the standard
            // ExceptionHandlerFilterAttribute cannot shape the response. Until server adopts
            // ProblemDetails (which the SDK default relies on), emit the same NotFound envelope
            // through Results so serialization follows the configured Http JsonOptions.
            options.OnFeatureCheckFailed = (context) =>
                Results.NotFound(new ErrorResponseModel("Resource not found."))
                    .ExecuteAsync(context.HttpContext);
        });

        // Server already has their own settings for the SdkKey and flag values
        // we map those existing settings to FeatureFlagOptions here
        services.AddOptions<FeatureFlagOptions>()
            .Configure<GlobalSettings, ILoggerFactory>((options, globalSettings, loggerFactory) =>
            {
                // Always prefer the SdkKey given in the new config location
                if (string.IsNullOrEmpty(options.LaunchDarkly.SdkKey))
                {
                    options.LaunchDarkly.SdkKey = globalSettings.LaunchDarkly.SdkKey;
                }

                // Self-hosted instances must never contact LaunchDarkly, even if an SdkKey was
                // carried over from a sample config or prior install. Clearing the key here forces
                // the SDK's LaunchDarklyClientProvider into offline mode.
                if (globalSettings.SelfHosted)
                {
                    options.LaunchDarkly.SdkKey = null;
                }

                // The legacy LaunchDarklyFeatureService loaded flag values from a JSON file pointed
                // at by FlagDataFilePath. The new SDK does not support that input - surface a warning
                // so any operator still relying on it migrates the values under Features:FlagValues.
                if (!string.IsNullOrEmpty(globalSettings.LaunchDarkly.FlagDataFilePath)
                    && File.Exists(globalSettings.LaunchDarkly.FlagDataFilePath))
                {
                    var logger = loggerFactory.CreateLogger(typeof(ServerSdkCompatibilityExtensions).FullName!);
                    logger.LogWarning(
                        "GlobalSettings:LaunchDarkly:FlagDataFilePath points at '{FlagDataFilePath}', but loading flag values from a file is no longer supported. Move these values under Features:FlagValues.",
                        globalSettings.LaunchDarkly.FlagDataFilePath);
                }

                // GlobalSettings flag values should not win over flag values added through the new way
                foreach (var (key, value) in globalSettings.LaunchDarkly.FlagValues)
                {
                    options.FlagValues.TryAdd(key, value);
                }
            });

        // Server has a class that contains all the feature flag keys
        // the application cares about, add them here.
        services.AddKnownFeatureFlags(FeatureFlagKeys.GetAllKeys());

        // ServerContextBuilder needs IHttpContextAccessor and resolves ICurrentContext per-request.
        // Every consuming Startup already registers ICurrentContext, but TryAdd lets this compat
        // layer stand on its own (e.g. in tests) without overriding any custom registrations.
        services.AddHttpContextAccessor();
        services.TryAddScoped<ICurrentContext, CurrentContext>();
        services.AddContextBuilder<ServerContextBuilder>();

        // Add the existing IFeatureService with a stub implementation that delegates to
        // the new IFeatureService under the hood. This should help ease migration but should
        // eventually go away
        services.TryAddScoped<Bit.Core.Services.IFeatureService, DelegatingFeatureService>();

        return services;
    }
}
