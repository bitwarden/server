using Bit.IntegrationTestCommon.Factories;
using Microsoft.AspNetCore.TestHost;

namespace Bit.SeederApi.IntegrationTest;

/// <summary>
/// A SeederApi factory with PlayId tracking enabled so that entities created during a play are recorded as
/// PlayItems and can be cleaned up via DELETE /seed/{playId}. Unlike <see cref="SeederApiApplicationFactory"/>
/// (which stubs a never-in-play service), this exercises the real tracking decorators and cascade cleanup.
/// </summary>
public class InPlaySeederApiApplicationFactory : WebApplicationFactoryBase<Startup>
{
    private readonly string _username = "username";
    private readonly string _password = "pass";

    public InPlaySeederApiApplicationFactory()
    {
        UpdateConfiguration("globalSettings:testPlayIdTrackingEnabled", "true");
        UpdateConfiguration("seederSettings:Username", _username);
        UpdateConfiguration("seederSettings:Password", _password);
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
