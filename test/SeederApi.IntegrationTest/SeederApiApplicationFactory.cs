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

        // Keep license signing deterministic regardless of a developer's local user secrets: with no
        // licensing certificate configured, the self-hosted premium path skips signing and warns.
        UpdateConfiguration("globalSettings:licenseCertificatePath", null);
        UpdateConfiguration("globalSettings:licenseCertificatePassword", null);
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
        ConfigureAccounts((username, password));
    }

    public void ConfigureAccounts(params (string Username, string Password)[] accounts)
    {
        UpdateConfiguration(builder =>
        {
            var entries = new Dictionary<string, string>();
            for (var i = 0; i < accounts.Length; i++)
            {
                entries[$"seederSettings:Accounts:{i}:Username"] = accounts[i].Username;
                entries[$"seederSettings:Accounts:{i}:Password"] = accounts[i].Password;
            }
            builder.AddInMemoryCollection(entries);
        });
    }
}
