
using System.CommandLine;
using Bit.DbMigration;

var fromProviderOption = new Option<ProviderOption>(
  new[] { "--from-provider", "-fp" },
  description: "The provider you would like to migrate data from."
);

var fromConnectionStringOption = new Option<string>(
  new[] { "--from-connection", "-fc" },
  description: "The connection string that should be used to get data from."
);

var toProviderOption = new Option<ToProviderOption>(
  new[] { "--to-provider", "-tp" },
  description: "The provider you would like to migrate data to."
);

var toConnectionStringOption = new Option<string>(
  new[] { "--to-connection", "-tc" },
  description: "The connection string that should be used to move data to."
);

var migrateCommand = new Command("migrate", "Command to migrate data from one database to another");
migrateCommand.AddOption(fromProviderOption);
migrateCommand.AddOption(fromConnectionStringOption);
migrateCommand.AddOption(toProviderOption);
migrateCommand.AddOption(toConnectionStringOption);

migrateCommand.SetHandler(MigrateHandler.RunAsync, fromProviderOption, fromConnectionStringOption, toProviderOption, toConnectionStringOption);

var providerOption = new Option<ProviderOption>(
  new[] { "--provider", "-p" },
  description: "The provider to clean"
);

var connectionStringOption = new Option<string>(
  new[] { "--connection", "-c" },
  description: "The connection string of the database to clean"
);

var areYouSureOption = new Option<bool>(
  "--yes-i-know-this-will-delete-everything-in-my-database",
  description: "Switch to add to skip the are you sure question."
);

var cleanCommand = new Command("clean", "Command to clean a target database");
cleanCommand.AddOption(providerOption);
cleanCommand.AddOption(connectionStringOption);
cleanCommand.AddOption(areYouSureOption);

cleanCommand.SetHandler(CleanHandler.RunAsync, providerOption, connectionStringOption, areYouSureOption);

var rootCommand = new RootCommand();
rootCommand.AddCommand(migrateCommand);
rootCommand.AddCommand(cleanCommand);

await rootCommand.InvokeAsync(args);

public enum ProviderOption
{
    SqlServer,
    Postgres,
    MySql,
    Sqlite,
}

// We can't support migrating TO SQL Server at this time. This enum should match the numbers of ProviderOption except
// for SQL Server
public enum ToProviderOption
{
    Postgres = 1,
    MySql = 2,
    Sqlite = 3,
}
