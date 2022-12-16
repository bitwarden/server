using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bit.DbMigration;

public static class Common
{
    public static IServiceProvider BuildContext(ProviderOption providerOption, string connectionString)
    {
        var services = new ServiceCollection();

        services.AddLogging(b =>
        {
            b.AddConsole();

            b.SetMinimumLevel(LogLevel.Warning);
        });

        services.AddDbContext<BitwardenVaultContext>(b =>
        {
            switch (providerOption)
            {
                case ProviderOption.Postgres:
                    b.UseNpgsql(connectionString);
                    break;
                case ProviderOption.MySql:
                    b.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
                    break;
                case ProviderOption.SqlServer:
                    b.UseSqlServer(connectionString);
                    break;
                case ProviderOption.Sqlite:
                    b.UseSqlite(connectionString);
                    break;
            }
        });

        return services.BuildServiceProvider();
    }
}
