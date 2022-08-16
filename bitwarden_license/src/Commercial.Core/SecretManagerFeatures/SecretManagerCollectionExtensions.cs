using Bit.Commercial.Core.SecretManagerFeatures.Secrets;
using Bit.Commercial.Core.SecretManagerFeatures.Secrets.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Commercial.Core.SecretManagerFeatures
{
    public static class SecretManagerCollectionExtensions
    {
        public static void AddSecretManagerServices(this IServiceCollection services)
        {
            services.AddScoped<ICreateSecretCommand, CreateSecretCommand>();
            services.AddScoped<IUpdateSecretCommand, UpdateSecretCommand>();
        }
    }
}

