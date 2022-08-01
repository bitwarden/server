using Bit.Core.SecretsManagerFeatures.AltPing;
using Bit.Core.SecretsManagerFeatures.AltPing.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.SecretsManagerFeatures;

public static class SecretsManagerServiceCollectionExtensions
{
    public static void AddSMFeaturesNoop(this IServiceCollection services)
    {
        services.AddScoped<IAltPingCommand, NoopAltPingCommand>();
    }
}
