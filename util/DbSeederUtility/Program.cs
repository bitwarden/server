using CommandDotNet;
using Bit.Seeder.Commands;
using Bit.Seeder.Settings;

namespace Bit.DbSeederUtility;

public class Program
{
    private static int Main(string[] args)
    {
        // Ensure global settings are loaded
        var globalSettings = GlobalSettingsFactory.GlobalSettings;
        
        // Set the current directory to the seeder directory for consistent seed paths
        var seederDirectory = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "seeder"));
        Directory.SetCurrentDirectory(seederDirectory);
        
        return new AppRunner<Program>()
            .Run(args);
    }

    [Command("generate", Description = "Generate seed data as JSON files")]
    public int Generate(
        [Option('u', "users", Description = "Number of users to generate")]
        int users,
        
        [Option('c', "ciphers-per-user", Description = "Number of ciphers per user to generate")]
        int ciphersPerUser,
        
        [Option('n', "seed-name", Description = "Name for the seed data files")]
        string seedName
    )
    {
        // Execute the generate command
        var generateCommand = new GenerateCommand();
        return generateCommand.Execute(users, ciphersPerUser, seedName, false) ? 0 : 1;
    }

    [Command("load", Description = "Load seed data from JSON files into the database")]
    public int Load(
        [Option('n', "seed-name", Description = "Name of the seed data to load")]
        string seedName,
        
        [Option('t', "timestamp", Description = "Specific timestamp of the seed data to load (defaults to most recent)")]
        string? timestamp = null,
        
        [Option('d', "dry-run", Description = "Validate the seed data without actually loading it")]
        bool dryRun = false
    )
    {
        // Execute the load command
        var loadCommand = new LoadCommand();
        return loadCommand.Execute(seedName, timestamp, dryRun) ? 0 : 1;
    }
    
    [Command("generate-direct-load", Description = "Generate seed data and load it directly into the database without creating JSON files")]
    public int GenerateDirectLoad(
        [Option('u', "users", Description = "Number of users to generate")]
        int users,
        
        [Option('c', "ciphers-per-user", Description = "Number of ciphers per user to generate")]
        int ciphersPerUser,
        
        [Option('n', "seed-name", Description = "Name identifier for this seed operation")]
        string seedName
    )
    {
        // Execute the generate command with loadImmediately=true
        var generateCommand = new GenerateCommand();
        return generateCommand.Execute(users, ciphersPerUser, seedName, true) ? 0 : 1;
    }
    
    [Command("extract", Description = "Extract data from the database into seed files")]
    public int Extract(
        [Option('n', "seed-name", Description = "Name for the extracted seed")]
        string seedName
    )
    {
        // Execute the extract command
        var extractCommand = new ExtractCommand();
        return extractCommand.Execute(seedName) ? 0 : 1;
    }
} 