using System.Text.Json;
using Bit.Core.Vault.Enums; // Change this line
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Infrastructure.EntityFramework.Vault.Models;
using Bogus;
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
                    SeedCiphers(context);

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
            Console.WriteLine($"Error adding users: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
            }
            throw; // Re-throw the exception to stop the seeding process
        }



        return true;
    }

    private void SeedUsers(DatabaseContext context)
    {
        if (!context.Users.Any())
        {
            Console.WriteLine("Generating 5000 users...");

            var faker = new Faker<Bit.Infrastructure.EntityFramework.Models.User>()
                .RuleFor(u => u.Id, f => Guid.NewGuid())
                .RuleFor(u => u.Name, f => f.Name.FullName())
                .RuleFor(u => u.Email, (f, u) => f.Internet.Email(u.Name))
                .RuleFor(u => u.EmailVerified, f => f.Random.Bool(0.9f))
                .RuleFor(u => u.SecurityStamp, f => Guid.NewGuid().ToString())
                .RuleFor(u => u.ApiKey, f => Guid.NewGuid().ToString("N").Substring(0, 30))
                .RuleFor(u => u.CreationDate, f => f.Date.Past(2))
                .RuleFor(u => u.RevisionDate, (f, u) => f.Date.Between(u.CreationDate, DateTime.UtcNow));

            var users = faker.Generate(5000);

            const int batchSize = 100;
            for (int i = 0; i < users.Count; i += batchSize)
            {
                context.Users.AddRange(users.Skip(i).Take(batchSize));
                context.SaveChanges();
                Console.WriteLine($"Added {Math.Min(i + batchSize, users.Count)} users");
            }

            Console.WriteLine("5000 test users added to the database.");
        }
        else
        {
            Console.WriteLine("Users table is not empty. Skipping user seeding.");
        }
    }

    private void SeedCiphers(DatabaseContext context)
    {
        if (!context.Ciphers.Any())
        {
            var users = context.Users.ToList();
            if (!users.Any())
            {
                Console.WriteLine("No users found. Please seed users first.");
                return;
            }

            Console.WriteLine($"Generating ciphers for {users.Count} users...");

            var faker = new Faker<Cipher>()
                .RuleFor(c => c.Id, f => Guid.NewGuid())
                .RuleFor(c => c.Type, f => CipherType.Login)
                .RuleFor(c => c.Data, f => JsonSerializer.Serialize(new
                {
                    Name = f.Internet.DomainName(),
                    Notes = f.Lorem.Sentence(),
                    Login = new
                    {
                        Username = f.Internet.UserName(),
                        Password = f.Internet.Password(),
                        Uri = f.Internet.Url()
                    }
                }))
                .RuleFor(c => c.CreationDate, f => f.Date.Past(1))
                .RuleFor(c => c.RevisionDate, (f, c) => f.Date.Between(c.CreationDate, DateTime.UtcNow))
                .RuleFor(c => c.DeletedDate, f => null)
                .RuleFor(c => c.Reprompt, f => CipherRepromptType.None);

            const int batchSize = 100;
            for (int i = 0; i < users.Count; i += batchSize)
            {
                var userBatch = users.Skip(i).Take(batchSize);
                var ciphers = userBatch.Select(user =>
                {
                    var cipher = faker.Generate();
                    cipher.UserId = user.Id;
                    return cipher;
                }).ToList();

                try
                {
                    context.Ciphers.AddRange(ciphers);
                    context.SaveChanges();
                    Console.WriteLine($"Added ciphers for users {i + 1} to {Math.Min(i + batchSize, users.Count)}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error adding ciphers: {ex.Message}");
                    if (ex.InnerException != null)
                    {
                        Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                    }
                    throw; // Re-throw the exception to stop the seeding process
                }
            }

            Console.WriteLine($"Ciphers added for all {users.Count} users.");
        }
        else
        {
            Console.WriteLine("Ciphers table is not empty. Skipping cipher seeding.");
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
