using Bit.GlobalSettingsBridge;
using Bitwarden.Server.Sdk.Environment.Setup;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

public static class GlobalSettingsBridgeServiceCollectionExtensions
{
    /// <summary>
    /// Registers options configurators that read from the <c>globalSettings</c> configuration
    /// section and populate <see cref="CorsOptions"/>, <see cref="ForwardedHeadersOptions"/>,
    /// and <see cref="SelfHostDetails"/>, replacing direct <c>GlobalSettings</c> dependencies
    /// in middleware setup.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This method is intended for services that already depend on GlobalSettings-style
    /// configuration and are migrating to the options pattern.</b> New services should configure
    /// <see cref="SelfHostDetails"/>, <see cref="CorsOptions"/>, and
    /// <see cref="ForwardedHeadersOptions"/> directly rather than going through this bridge.
    /// </para>
    /// <para>
    /// After calling this method, configure CORS middleware with the parameterless overload
    /// so it picks up the default policy populated here:
    /// <code>app.UseCors();</code>
    /// instead of the inline-lambda form currently used at each call site.
    /// </para>
    /// <para>
    /// Forwarded-headers options are always configured by this method; the caller is still
    /// responsible for guarding the middleware behind a self-hosted check:
    /// <code>
    /// if (environment.SelfHosted)
    /// {
    ///     app.UseForwardedHeaders();
    /// }
    /// </code>
    /// </para>
    /// <para>
    /// This method depends on <see cref="IBitwardenEnvironment"/> being resolvable from the
    /// container. Call <c>AddBitwardenEnvironment()</c> (or <c>UseBitwardenSdk()</c>) before
    /// or after this method — registration order does not matter because both are resolved
    /// lazily when options are first accessed.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddGlobalSettingsBridge(this IServiceCollection services)
    {
        services.TryAddEnumerable([
            ServiceDescriptor.Singleton<IConfigureOptions<SelfHostDetails>, ConfigureSelfHostDetails>(),
            ServiceDescriptor.Singleton<IConfigureOptions<CorsOptions>, ConfigureCorsOptions>(),
            ServiceDescriptor.Singleton<IConfigureOptions<ForwardedHeadersOptions>, ConfigureForwardedHeadersOptions>(),
        ]);

        return services;
    }
}
