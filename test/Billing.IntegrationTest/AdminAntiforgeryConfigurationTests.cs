using Bit.Admin.Jobs;
using Bit.IntegrationTestCommon;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bit.Billing.IntegrationTest;

/// <summary>
/// Guards the fix for VULN-826: the Admin MVC pipeline must register a global
/// <see cref="AutoValidateAntiforgeryTokenAttribute"/> so antiforgery protection
/// is default-on and a newly added Admin action inherits it without opting in.
/// </summary>
public class AdminAntiforgeryConfigurationTests
{
    [Fact]
    public void AdminPipeline_RegistersGlobalAntiforgeryFilter()
    {
        ITestDatabase testDatabase = new SqliteTestDatabase();
        try
        {
            using var factory = new WebApplicationFactory<Admin.Program>().WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    var configValues = new Dictionary<string, string?>
                    {
                        ["globalSettings:databaseProvider"] = "sqlite",
                    };
                    testDatabase.ModifyGlobalSettings(configValues);
                    config.AddInMemoryCollection(configValues);
                });

                builder.ConfigureServices(services =>
                {
                    var jobHostedServiceDescriptor = services.Single(sd => sd.ImplementationType == typeof(JobsHostedService));
                    services.Remove(jobHostedServiceDescriptor);
                    testDatabase.AddDatabase(services);
                });
            });

            var mvcOptions = factory.Services.GetRequiredService<IOptions<MvcOptions>>().Value;

            Assert.Contains(mvcOptions.Filters, filter => filter is AutoValidateAntiforgeryTokenAttribute);
        }
        finally
        {
            testDatabase.Dispose();
        }
    }
}
