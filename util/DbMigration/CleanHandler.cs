using Microsoft.Extensions.DependencyInjection;

namespace Bit.DbMigration;

public static class CleanHandler
{
    public static async Task RunAsync(ProviderOption providerOption, string connectionString, bool areYouSure)
    {
        if (!areYouSure)
        {
            Console.Write("This operation will delete everything in your database, are you sure you want to continue? [Y,n]: ");
            var response = Console.ReadLine();
            if (!(response?.StartsWith("Y", StringComparison.InvariantCultureIgnoreCase) == true))
            {
                return;
            }
        }

        var services = Common.BuildContext(providerOption, connectionString);

        var context = services.GetRequiredService<BitwardenVaultContext>();

        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
    }
}
