using Bit.IntegrationTestCommon.Factories;
using Microsoft.AspNetCore.TestHost;

namespace Bit.SeederApi.IntegrationTest;

/// <summary>
/// A self-hosted SeederApi factory with PlayId tracking enabled, so a test class can share one host and clean up
/// its own data via DELETE /seed/{playId}.
/// </summary>
public class SelfHostedSeederApiApplicationFactory : WebApplicationFactoryBase<Startup>
{
    private readonly string _username = "username";
    private readonly string _password = "pass";

    public SelfHostedSeederApiApplicationFactory()
    {
        UpdateConfiguration("globalSettings:testPlayIdTrackingEnabled", "true");
        UpdateConfiguration("seederSettings:Accounts:0:Username", _username);
        UpdateConfiguration("seederSettings:Accounts:0:Password", _password);
        UpdateConfiguration("globalSettings:selfHosted", "true");
        // AddPush rejects self-hosted startup without an installation id.
        UpdateConfiguration("globalSettings:installation:id", "10000000-0000-0000-0000-000000000000");
        // Self-hosted defaults data protection keys to /etc/bitwarden, which is not writable on CI.
        UpdateConfiguration("globalSettings:dataProtection:directory",
            Path.Combine(Path.GetTempPath(), $"seeder-dp-{Guid.NewGuid()}"));
        UpdateConfiguration("globalSettings:licenseCertificatePath", null);
        UpdateConfiguration("globalSettings:licenseCertificatePassword", null);
    }

    public string Username => _username;
    public string Password => _password;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureTestServices(services =>
        {
            // Remove scheduled background jobs to prevent errors in parallel test execution
            var jobService = services.First(sd =>
                sd.ServiceType == typeof(IHostedService) &&
                sd.ImplementationType == typeof(Jobs.JobsHostedService));
            services.Remove(jobService);
        });
    }
}
