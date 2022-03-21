using System;
using System.Collections.Generic;
using System.Reflection;
using Bit.Core.Settings;
using Bit.SharedWeb.Utilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Sdk;

namespace Bit.Core.IntegrationTest
{
    public class RepositoryDataAttribute : DataAttribute
    {
        public override IEnumerable<object[]> GetData(MethodInfo testMethod)
        {
            var services = CreateServiceProvider();

            var parameterInfos = testMethod.GetParameters();
            var parameters = new object[parameterInfos.Length];

            for (var i = 0; i < parameterInfos.Length; i++)
            {
                var parameterType = parameterInfos[i].ParameterType;

                if (parameterType == typeof(IServiceProvider))
                {
                    parameters[i] = services;
                    continue;
                }

                parameters[i] = services.GetRequiredService(parameterType);
            }

            return new []
            {
                parameters,
            };
        }

        private static IServiceProvider CreateServiceProvider()
        {
            var connStr = Environment.GetEnvironmentVariable("INTTEST_CONNSTR");
            var provider = Environment.GetEnvironmentVariable("INTTEST_PROVIDER");

            var inMemConfig = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("globalSettings:databaseProvider", provider),
                new KeyValuePair<string, string>("globalSettings:sqlServer:connectionString", connStr),
                new KeyValuePair<string, string>("globalSettings:postgresSql:connectionString", connStr),
                new KeyValuePair<string, string>("globalSettings:mySql:connectionString", connStr),
            };

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemConfig)
                .Build();

            var services = new ServiceCollection();

            var globalSettings = config.GetSection("globalSettings").Get<GlobalSettings>();

            services.AddSingleton<IConfiguration>(config);
            services.AddSingleton(globalSettings);

            services.AddSqlServerRepositories(globalSettings);

            return services.BuildServiceProvider();
        }
    }
}
