using Bit.DBSeeder;
using CommandDotNet;

internal class Program
{
    private static int Main(string[] args)
    {
        return new AppRunner<Program>().Run(args);
    }

    [DefaultCommand]
    public void Execute(
        
        
        
     
        
        
        [Operand(Description = "Database provider (mssql, mysql, postgres, sqlite).")] string databaseProvider,
        [Operand(Description = "Database connection string.")] string ConnectionString
       
        
        
        
        
        ) => SeedDatabase(ConnectionString, databaseProvider);

    private static bool SeedDatabase(string databaseConnectionString,
   string databaseProvider)
{
    var seeder = new EFDBSeeder(databaseConnectionString, databaseProvider);
    bool success;
 
    success = seeder.SeedDatabase(); // Change this line
    

    return success;
}
}