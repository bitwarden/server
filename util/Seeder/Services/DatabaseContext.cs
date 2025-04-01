using Bit.Core.Entities;
using Bit.Core.Settings;
using Bit.Core.Vault.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bit.Seeder.Services;

public class DatabaseContext : DbContext
{
    private readonly GlobalSettings _globalSettings;

    public DatabaseContext(GlobalSettings globalSettings)
    {
        _globalSettings = globalSettings;
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Cipher> Ciphers { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        var provider = _globalSettings.DatabaseProvider ?? string.Empty;
        Console.WriteLine($"Database Provider: '{provider}'");

        // Output all available connection strings for debugging
        Console.WriteLine($"SqlServer ConnectionString available: {!string.IsNullOrEmpty(_globalSettings.SqlServer?.ConnectionString)}");
        Console.WriteLine($"PostgreSql ConnectionString available: {!string.IsNullOrEmpty(_globalSettings.PostgreSql?.ConnectionString)}");
        Console.WriteLine($"MySql ConnectionString available: {!string.IsNullOrEmpty(_globalSettings.MySql?.ConnectionString)}");
        Console.WriteLine($"Sqlite ConnectionString available: {!string.IsNullOrEmpty(_globalSettings.Sqlite?.ConnectionString)}");

        var connectionString = _globalSettings.DatabaseProvider switch
        {
            "postgres" => _globalSettings.PostgreSql?.ConnectionString,
            "mysql" => _globalSettings.MySql?.ConnectionString,
            "sqlite" => _globalSettings.Sqlite?.ConnectionString,
            _ => _globalSettings.SqlServer?.ConnectionString
        };

        Console.WriteLine($"Using connection string: {connectionString}");

        switch (_globalSettings.DatabaseProvider)
        {
            case "postgres":
                optionsBuilder.UseNpgsql(connectionString);
                break;
            case "mysql":
                optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
                break;
            case "sqlite":
                optionsBuilder.UseSqlite(connectionString);
                break;
            default:
                optionsBuilder.UseSqlServer(connectionString);
                break;
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Cipher>(entity =>
        {
            entity.ToTable("Cipher");
            entity.Property(e => e.Type).HasConversion<int>();
            entity.Property(e => e.Reprompt).HasConversion<int>();
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("User");
            entity.Property(e => e.Kdf).HasConversion<int>();
        });
    }
}
