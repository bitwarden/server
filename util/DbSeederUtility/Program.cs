using Bit.Seeder.Commands;
using Bit.Seeder.Settings;
using CommandDotNet;

namespace Bit.DbSeederUtility;

public class Program
{
    private static int Main(string[] args)
    {
        // Ensure global settings are loaded
        var globalSettings = GlobalSettingsFactory.GlobalSettings;

        return new AppRunner<Program>()
            .Run(args);
    }

    [Command("organization", Description = "Seed an organization and organization users")]
    public int Organization(
        [Option('n', "Name", Description = "Name of organization")]
        string name,

        [Option('u', "users", Description = "Number of users to generate")]
        int users,

        [Option('d', "domain", Description = "Email domain for users")]
        string domain
    )
    {
        var generateCommand = new GenerateCommand();
        return generateCommand.Execute(name, users, domain) ? 0 : 1;
    }
}
