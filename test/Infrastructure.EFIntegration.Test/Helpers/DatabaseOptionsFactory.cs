using System.Text;
using Bit.Core;
using Bit.Core.Test.Helpers.Factories;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Bit.Infrastructure.EFIntegration.Test.Helpers;

public static class DatabaseOptionsFactory
{
    public static List<DbContextOptions<DatabaseContext>> Options { get; } = new();

    static DatabaseOptionsFactory()
    {
        var services = new ServiceCollection()
            .AddSingleton(sp =>
            {
                var dataProtector = Substitute.For<IDataProtector>();
                dataProtector.Unprotect(Arg.Any<byte[]>())
                    .Returns<byte[]>(data =>
                        Encoding.UTF8.GetBytes(Constants.DatabaseFieldProtectedPrefix +
                                               Encoding.UTF8.GetString((byte[])data[0])));

                var dataProtectionProvider = Substitute.For<IDataProtectionProvider>();
                dataProtectionProvider.CreateProtector(Constants.DatabaseFieldProtectorPurpose)
                    .Returns(dataProtector);

                return dataProtectionProvider;
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
