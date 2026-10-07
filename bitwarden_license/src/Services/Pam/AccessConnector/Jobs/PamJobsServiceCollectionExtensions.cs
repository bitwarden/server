namespace Bit.Services.Pam.AccessConnector.Jobs;

/// <summary>Registers the PAM sweep services and the Quartz jobs <c>JobsHostedService</c> schedules.</summary>
public static class PamJobsServiceCollectionExtensions
{
    public static IServiceCollection AddPamJobServices(this IServiceCollection services)
    {
        services.AddScoped<IPamRotationSweepService, PamRotationSweepService>();
        services.AddScoped<IPamLeaseExpirySweepService, PamLeaseExpirySweepService>();

        services.AddTransient<PamRotationSweepJob>();
        services.AddTransient<PamLeaseExpirySweepJob>();

        return services;
    }
}
