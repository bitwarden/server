using Bit.Core.Services;
using Bit.IntegrationTestCommon;
using Bit.IntegrationTestCommon.Factories;
using Microsoft.AspNetCore.TestHost;

namespace Bit.SeederApi.IntegrationTest;

public class SeederApiApplicationFactory : WebApplicationFactoryBase<Startup>
{
    public SeederApiApplicationFactory()
    {
        TestDatabase = new SqliteTestDatabase();
        _configureTestServices.Add(serviceCollection =>
        {
            serviceCollection.AddSingleton<IPlayIdService, NeverPlayIdServices>();
            serviceCollection.AddHttpContextAccessor();
        });
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureTestServices(services =>
        {
            // Remove scheduled background jobs to prevent errors in parallel test execution
            var jobService = services.First(sd => sd.ServiceType == typeof(IHostedService) && sd.ImplementationType == typeof(Jobs.JobsHostedService));
            services.Remove(jobService);
        });
    }

    public void ConfigureAuth(string username, string password)
    {
        UpdateConfiguration(builder =>
        {
            builder.AddInMemoryCollection(new Dictionary<string, string>
            {
                { "seederSettings:Username", username},
                { "seederSettings:Password", password}
            });
        });
    }
}
