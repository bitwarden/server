using Bit.CommCore.SecretsManagerFeatures.AltPing;
using Bit.Core.SecretsManagerFeatures.AltPing.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.CommCore.SecretsManagerFeatures;

public static class SecretsManagerServiceCollectionExtension
{
    public static void AddSMFeatures(this IServiceCollection services)
    {
        services.AddScoped<IAltPingCommand, AltPingCommand>();
    }
}
