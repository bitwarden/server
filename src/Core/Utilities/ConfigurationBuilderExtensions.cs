using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace Bit.Core.Utilities
{
    public static class ConfigurationBuilderExtensions
    {
        public static IConfigurationBuilder AddSettingsConfiguration(
            this ConfigurationBuilder builder,
            IHostingEnvironment env,
            string userSecretsId)
        {
            builder.SetBasePath(env.ContentRootPath)
                .AddJsonFile("settings.json")
                .AddJsonFile($"settings.{env.EnvironmentName}.json", optional: true);

            if(env.IsDevelopment())
            {
                builder.AddUserSecrets(userSecretsId);
            }

            builder.AddEnvironmentVariables();

            return builder;
        }
    }
}
