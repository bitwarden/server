using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace Bit.Core.Utilities
{
    public static class ConfigurationBuilderExtensions
    {
        public static IConfigurationBuilder AddSettingsConfiguration<T>(
            this ConfigurationBuilder builder,
            IHostingEnvironment env) where T : class
        {
            builder.SetBasePath(env.ContentRootPath)
                .AddJsonFile("settings.json")
                .AddJsonFile($"settings.{env.EnvironmentName}.json", optional: true);

            if(env.IsDevelopment())
            {
                builder.AddUserSecrets<T>();
            }

            builder.AddEnvironmentVariables();

            return builder;
        }
    }
}
