#nullable enable

using Bit.Infrastructure.EntityFramework.Models;
using Bit.Infrastructure.EntityFramework.Vault.Models;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Seeder.Factories;
using Bit.Seeder.Services;
using LinqToDB.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

namespace Bit.Seeder.Recipes;

/// <summary>
/// Recipe for creating users with vault items in a single operation,
/// ensuring the same encryption key is used for both user and items
/// </summary>
public class UserWithVaultItemsRecipe(DatabaseContext db)
{
    /// <summary>
    /// Creates a user with vault items using the same encryption key
    /// </summary>
    public async Task<(User user, int itemCount)> CreateUserWithItems(
        string email,
        string password,
        ISeederCryptoService cryptoService,
        IDataProtectionService dataProtection,
        IPasswordHasher<User> passwordHasher,
        int loginCount = 10,
        int noteCount = 5,
        int cardCount = 3,
        int identityCount = 2)
    {
        // Generate user key - THIS IS THE KEY WE'LL USE FOR EVERYTHING
        var userKey = cryptoService.GenerateUserKey();
        
        // Create the user with the same key
        var user = UserSeeder.CreateUser(email, password, cryptoService, dataProtection, passwordHasher, userKey);
        
        // Save user first
        db.Users.Add(user);
        await db.SaveChangesAsync();
        
        // Now create vault items with THE SAME userKey
        var ciphers = new List<Cipher>();
        var random = new Random();
        
        // Create login items
        var logins = CipherSeeder.SampleData.Logins.OrderBy(_ => random.Next()).Take(loginCount);
        foreach (var (name, username, uri, samplePassword) in logins)
        {
            var cipher = CipherSeeder.CreateLogin(
                name, username, samplePassword, uri,
                user.Id, null, userKey, cryptoService,
                notes: $"Created with {email}");
            ciphers.Add(cipher);
        }
        
        // Create secure notes
        var notes = CipherSeeder.SampleData.SecureNotes.OrderBy(_ => random.Next()).Take(noteCount);
        foreach (var (name, content) in notes)
        {
            var cipher = CipherSeeder.CreateSecureNote(
                name, content,
                user.Id, null, userKey, cryptoService);
            ciphers.Add(cipher);
        }
        
        // Create cards
        var cards = CipherSeeder.SampleData.Cards.OrderBy(_ => random.Next()).Take(cardCount);
        foreach (var (name, number, holder, brand) in cards)
        {
            var cipher = CipherSeeder.CreateCard(
                name, holder, number, brand,
                "12", "2028", "123",
                user.Id, null, userKey, cryptoService,
                notes: "Test card - do not use");
            ciphers.Add(cipher);
        }
        
        // Create identities
        var identities = CipherSeeder.SampleData.Identities.OrderBy(_ => random.Next()).Take(identityCount);
        foreach (var (title, first, last, identityEmail) in identities)
        {
            var cipher = CipherSeeder.CreateIdentity(
                $"{first} {last} Identity",
                title, first, last, identityEmail, "555-0100",
                user.Id, null, userKey, cryptoService,
                notes: "Sample identity data");
            ciphers.Add(cipher);
        }
        
        // Bulk insert vault items
        if (ciphers.Any())
        {
            await db.BulkCopyAsync(ciphers);
        }
        
        Console.WriteLine($"✅ Created user {email} with {ciphers.Count} vault items");
        Console.WriteLine($"   Password: {password}");
        Console.WriteLine($"   Logins: {loginCount}");
        Console.WriteLine($"   Secure Notes: {noteCount}");
        Console.WriteLine($"   Cards: {cardCount}");
        Console.WriteLine($"   Identities: {identityCount}");
        
        return (user, ciphers.Count);
    }
    
    /// <summary>
    /// Creates multiple users with vault items
    /// </summary>
    public async Task<int> CreateMultipleUsersWithItems(
        string emailPrefix,
        string domain,
        string password,
        int userCount,
        ISeederCryptoService cryptoService,
        IDataProtectionService dataProtection,
        IPasswordHasher<User> passwordHasher,
        int itemsPerUser = 5)
    {
        var totalItems = 0;
        
        for (int i = 1; i <= userCount; i++)
        {
            var email = $"{emailPrefix}{i}@{domain}";
            var (user, itemCount) = await CreateUserWithItems(
                email,
                password,
                cryptoService,
                dataProtection,
                passwordHasher,
                loginCount: itemsPerUser,
                noteCount: 2,
                cardCount: 1,
                identityCount: 1);
                
            totalItems += itemCount;
        }
        
        return totalItems;
    }
}