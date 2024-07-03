using System;
using System.Linq;
using System.Data;
using System.Reflection;
using System.Text;
using Bit.Core;
using Bit.Infrastructure.EntityFramework.Models; // Change this line
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Bit.DBSeeder;

public class EFDBSeeder
{
    private readonly string _connectionString;
    private readonly string _databaseProvider;
    private readonly ILogger<EFDBSeeder> _logger;

    public EFDBSeeder(string connectionString, string databaseProvider)
    {
        _connectionString = connectionString;
        _databaseProvider = databaseProvider;
       
    }

    public bool SeedDatabase()
    {
       //print connectionstring to console
        Console.WriteLine(_connectionString);
        Console.WriteLine(_databaseProvider);
        
    try
        {
            var factory = new DatabaseContextFactory();
            using (var context = factory.CreateDbContext(new[] { _connectionString }))
            {
                if (context.Database.CanConnect())
                {
                    Console.WriteLine("Successfully connected to the database!");
                    
                    // Seed the database
                    SeedUsers(context);
                    
                    Console.WriteLine("Database seeded successfully!");
                }
                else
                {
                    Console.WriteLine("Failed to connect to the database.");
                    return false;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
            return false;
        }
        
        
        
        return true;
    }

    private void SeedUsers(DatabaseContext context)
    {
        if (!context.Users.Any())
        {
            context.Users.AddRange(
            new Bit.Infrastructure.EntityFramework.Models.User // Specify the full namespace
            { 
                Id = Guid.NewGuid(), 
                Name = "Test User", 
                Email = "testuser@example.com", 
                EmailVerified = true, 
                SecurityStamp = Guid.NewGuid().ToString(), 
                ApiKey = "TestApiKey" 
            }
            );
            context.SaveChanges();
            Console.WriteLine("Test user added to the database.");
        }
        else
        {
            Console.WriteLine("Users table is not empty. Skipping user seeding.");
        }
    }

   /* private ILogger<EFDBSeeder> CreateLogger()
    {
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .AddFilter("Microsoft", LogLevel.Warning)
                .AddFilter("System", LogLevel.Warning)
                .AddConsole();

            builder.AddFilter("EFDBSeeder.EFDBSeeder", LogLevel.Information);
        });

        return loggerFactory.CreateLogger<EFDBSeeder>();
    }
    */
}
