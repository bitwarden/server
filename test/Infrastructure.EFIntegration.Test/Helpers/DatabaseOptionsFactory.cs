using System.Text;
using Bit.Core;
using Bit.Core.Test.Helpers.Factories;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Bit.Infrastructure.EFIntegration.Test.Helpers;

public static class DatabaseOptionsFactory
{
    public static List<DbContextOptions<DatabaseContext>> Options { get; } = new();

    static DatabaseOptionsFactory()
    {
        var services = new ServiceCollection()
            .AddSingleton(sp =>
            {
                var dataProtector = new Mock<IDataProtector>();
                dataProtector
                    .Setup(d => d.Unprotect(It.IsAny<byte[]>()))
                    .Returns<byte[]>(data => Encoding.UTF8.GetBytes("P|" + Encoding.UTF8.GetString(data))); // I THINK?

                var dataProtectionProvider = new Mock<IDataProtectionProvider>();
                dataProtectionProvider
                    .Setup(x => x.CreateProtector(Constants.DatabaseFieldProtectorPurpose))
                    .Returns(dataProtector.Object);

                return dataProtectionProvider.Object;
            })
            .BuildServiceProvider();

        var globalSettings = GlobalSettingsFactory.GlobalSettings;
        if (!string.IsNullOrWhiteSpace(GlobalSettingsFactory.GlobalSettings.PostgreSql?.ConnectionString))
        {
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
            Options.Add(new DbContextOptionsBuilder<DatabaseContext>()
                .UseNpgsql(globalSettings.PostgreSql.ConnectionString)
                .UseApplicationServiceProvider(services)
                .Options);
        }
        if (!string.IsNullOrWhiteSpace(GlobalSettingsFactory.GlobalSettings.MySql?.ConnectionString))
        {
            var mySqlConnectionString = globalSettings.MySql.ConnectionString;
            Options.Add(new DbContextOptionsBuilder<DatabaseContext>()
                .UseMySql(mySqlConnectionString, ServerVersion.AutoDetect(mySqlConnectionString))
                .UseApplicationServiceProvider(services)
                .Options);
        }
    }
}
