using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Bit.Core.Entities;

namespace EFDBSeederUtility
{
    public static class SeedData
    {
        public static void Initialize(IServiceProvider serviceProvider)
        {
            using (var context = new DatabaseContext(
                serviceProvider.GetRequiredService<DbContextOptions<DatabaseContext>>()))
            {
                if (!context.Users.Any())
                {
                    context.Users.Add(new User
                    {
                        Id = Guid.NewGuid(), 
                        Name = "Test User", 
                        Email = "testuser@example.com", 
                        EmailVerified = true, 
                        SecurityStamp = Guid.NewGuid().ToString(), 
                        ApiKey = "TestApiKey" 
                    });
                    context.SaveChanges();
                }

                // Add other seed data here
            }
        }
    }
}
