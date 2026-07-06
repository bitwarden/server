namespace Bit.Services.Pam.Rotation.Jobs;

/// <summary>
/// Registers the PAM rotation Quartz sweep services and jobs. Kept separate from
/// <c>Bit.Services.Pam.Utilities.ServiceCollectionExtensions.AddPamServices</c> because the jobs are commercial-gated
/// (see <c>JobsHostedService</c>'s <c>#if !OSS</c> block) and registered from <c>Startup</c>'s non-OSS branch
/// alongside <c>JobsHostedService.AddCommercialSecretsManagerJobServices</c>, not from <c>AddPamServices</c> itself.
/// </summary>
public static class PamJobsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the two sweep services (scoped -- they depend on scoped repositories/commands) and the two Quartz
    /// job classes (transient -- Quartz's DI job factory resolves a new instance per firing, the same lifetime
    /// every other job in this codebase uses).
    /// </summary>
    public static IServiceCollection AddPamJobServices(this IServiceCollection services)
    {
        services.AddScoped<IPamRotationSweepService, PamRotationSweepService>();
        services.AddScoped<IPamLeaseExpirySweepService, PamLeaseExpirySweepService>();

        services.AddTransient<PamRotationSweepJob>();
        services.AddTransient<PamLeaseExpirySweepJob>();

        return services;
    }
}
