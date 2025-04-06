using Bit.Infrastructure.EntityFramework.Models;
using Bit.Seeder.Factories;
using Bit.Seeder.Settings;
using Bit.SharedWeb.Utilities;
using LinqToDB.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using DatabaseContext = Bit.Infrastructure.EntityFramework.Repositories.DatabaseContext;

namespace Bit.Seeder.Commands;

public class GenerateCommand
{
    public bool Execute(string name, int users, string domain)
    {
        // Create service provider with necessary services
        var services = new ServiceCollection();
        ConfigureServices(services);
        var serviceProvider = services.BuildServiceProvider();

        var logger = serviceProvider.GetRequiredService<ILogger<GenerateCommand>>();

        var organization = OrganizationSeeder.CreateEnterprise(name, domain, users);
        var user = UserSeeder.CreateUser($"admin@{domain}");
        var orgUser = organization.CreateOrganizationUser(user);

        var additionalUsers = new List<User>();
        var additionalOrgUsers = new List<OrganizationUser>();
        for (var i = 0; i < users; i++)
        {
            var additionalUser = UserSeeder.CreateUser($"user{i}@{domain}");
            additionalUsers.Add(additionalUser);
            additionalOrgUsers.Add(organization.CreateOrganizationUser(additionalUser));
        }


        using (var scope = serviceProvider.CreateScope())
        {
            var scopedServices = scope.ServiceProvider;
            var db = scopedServices.GetRequiredService<DatabaseContext>();

            db.Add(organization);
            db.Add(user);
            db.Add(orgUser);

            db.SaveChanges();

            // Use LinqToDB's BulkCopy for significant better performance
            db.BulkCopy(additionalUsers);
            db.BulkCopy(additionalOrgUsers);
        }

        return true;

    }

    private void ConfigureServices(ServiceCollection services)
    {
        // Load configuration using the GlobalSettingsFactory
        var globalSettings = GlobalSettingsFactory.GlobalSettings;

        // Register services
        services.AddLogging(builder => builder.AddConsole());
        services.AddSingleton(globalSettings);

        // Add Data Protection services
        services.AddDataProtection()
            .SetApplicationName("Bitwarden");

        services.AddDatabaseRepositories(globalSettings);
    }
}
