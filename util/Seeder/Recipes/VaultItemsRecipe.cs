#nullable enable

using Bit.Infrastructure.EntityFramework.Models;
using Bit.Infrastructure.EntityFramework.Vault.Models;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Seeder.Factories;
using Bit.Seeder.Services;
using LinqToDB.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Bit.Seeder.Recipes;

/// <summary>
/// Recipe for creating vault items (ciphers) for users or organizations
/// </summary>
public class VaultItemsRecipe(DatabaseContext db)
{
    /// <summary>
    /// Seeds vault items for a specific user
    /// </summary>
    public async Task<int> SeedUserItems(
        Guid userId,
        byte[] userKey,
        ISeederCryptoService cryptoService,
        int loginCount = 10,
        int noteCount = 5,
        int cardCount = 3,
        int identityCount = 2)
    {
        var ciphers = new List<Cipher>();
        var random = new Random();
        
        // Create login items
        var logins = CipherSeeder.SampleData.Logins.OrderBy(_ => random.Next()).Take(loginCount);
        foreach (var (name, username, uri, password) in logins)
        {
            var cipher = CipherSeeder.CreateLogin(
                name, username, password, uri,
                userId, null, userKey, cryptoService,
                notes: $"Auto-generated login for {name}");
            ciphers.Add(cipher);
        }
        
        // Create secure notes
        var notes = CipherSeeder.SampleData.SecureNotes.OrderBy(_ => random.Next()).Take(noteCount);
        foreach (var (name, content) in notes)
        {
            var cipher = CipherSeeder.CreateSecureNote(
                name, content,
                userId, null, userKey, cryptoService);
            ciphers.Add(cipher);
        }
        
        // Create cards
        var cards = CipherSeeder.SampleData.Cards.OrderBy(_ => random.Next()).Take(cardCount);
        foreach (var (name, number, holder, brand) in cards)
        {
            var cipher = CipherSeeder.CreateCard(
                name, holder, number, brand,
                "12", "2028", "123",
                userId, null, userKey, cryptoService,
                notes: "Test card - do not use");
            ciphers.Add(cipher);
        }
        
        // Create identities
        var identities = CipherSeeder.SampleData.Identities.OrderBy(_ => random.Next()).Take(identityCount);
        foreach (var (title, first, last, email) in identities)
        {
            var cipher = CipherSeeder.CreateIdentity(
                $"{first} {last} Identity",
                title, first, last, email, "555-0100",
                userId, null, userKey, cryptoService,
                notes: "Sample identity data");
            ciphers.Add(cipher);
        }
        
        // Bulk insert for performance
        await db.BulkCopyAsync(ciphers);
        
        Console.WriteLine($"✅ Created {ciphers.Count} vault items for user");
        Console.WriteLine($"   Logins: {loginCount}");
        Console.WriteLine($"   Secure Notes: {noteCount}");
        Console.WriteLine($"   Cards: {cardCount}");
        Console.WriteLine($"   Identities: {identityCount}");
        
        return ciphers.Count;
    }
    
    /// <summary>
    /// Seeds vault items for an organization
    /// </summary>
    public async Task<int> SeedOrganizationItems(
        Guid organizationId,
        byte[] organizationKey,
        ISeederCryptoService cryptoService,
        Guid? collectionId = null,
        int sharedLoginCount = 5,
        int sharedNoteCount = 3)
    {
        var ciphers = new List<Cipher>();
        var random = new Random();
        
        // Create shared login items
        var logins = CipherSeeder.SampleData.Logins.OrderBy(_ => random.Next()).Take(sharedLoginCount);
        foreach (var (name, username, uri, password) in logins)
        {
            var cipher = CipherSeeder.CreateLogin(
                $"[Shared] {name}", username, password, uri,
                null, organizationId, organizationKey, cryptoService,
                notes: $"Shared organizational login for {name}");
            ciphers.Add(cipher);
        }
        
        // Create shared notes
        var notes = new[]
        {
            ("Team Passwords", "Production DB: ProdPass#2024\nStaging DB: StagePass#2024\nDev DB: DevPass#2024"),
            ("Emergency Contacts", "IT Support: +1-555-0199\nSecurity Team: security@company.com\nOn-call: +1-555-0911"),
            ("Deployment Guide", "1. Check CI/CD pipeline\n2. Review staging tests\n3. Deploy to production\n4. Monitor for 30 minutes")
        };
        
        foreach (var (name, content) in notes.Take(sharedNoteCount))
        {
            var cipher = CipherSeeder.CreateSecureNote(
                $"[Team] {name}", content,
                null, organizationId, organizationKey, cryptoService);
            ciphers.Add(cipher);
        }
        
        // Bulk insert ciphers
        await db.BulkCopyAsync(ciphers);
        
        // TODO: Add collection support when CollectionCipher model is available
        // if (collectionId.HasValue)
        // {
        //     // Add ciphers to collection
        // }
        
        Console.WriteLine($"✅ Created {ciphers.Count} vault items for organization");
        Console.WriteLine($"   Shared Logins: {sharedLoginCount}");
        Console.WriteLine($"   Shared Notes: {sharedNoteCount}");
        
        return ciphers.Count;
    }
    
    /// <summary>
    /// Seeds vault items for all users in an organization
    /// </summary>
    public async Task<int> SeedOrganizationUserItems(
        string organizationName,
        ISeederCryptoService cryptoService,
        IDataProtectionService dataProtection,
        int itemsPerUser = 5)
    {
        // Get organization and its users
        var org = await db.Organizations
            .FirstOrDefaultAsync(o => o.Name == organizationName);
            
        if (org == null)
        {
            throw new ArgumentException($"Organization '{organizationName}' not found");
        }
        
        var orgUsers = await db.OrganizationUsers
            .Where(ou => ou.OrganizationId == org.Id && ou.Status == Core.Enums.OrganizationUserStatusType.Confirmed)
            .ToListAsync();
            
        // Load users separately
        var userIds = orgUsers.Select(ou => ou.UserId).Where(id => id.HasValue).Select(id => id!.Value).ToList();
        var users = await db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id);
            
        Console.WriteLine($"Found {orgUsers.Count} users in organization '{organizationName}'");
        
        var totalItems = 0;
        
        // For each user, create personal items
        foreach (var orgUser in orgUsers)
        {
            if (!orgUser.UserId.HasValue || !users.TryGetValue(orgUser.UserId.Value, out var user)) 
                continue;
            
            // For this example, we'll use a dummy user key
            // In production, you'd decrypt the actual user key
            var userKey = cryptoService.GenerateUserKey();
            
            var itemCount = await SeedUserItems(
                orgUser.UserId.Value,
                userKey,
                cryptoService,
                loginCount: itemsPerUser,
                noteCount: 2,
                cardCount: 1,
                identityCount: 1);
                
            totalItems += itemCount;
            Console.WriteLine($"   Created {itemCount} items for {user.Email}");
        }
        
        // Also create some shared organizational items
        var orgKey = cryptoService.GenerateOrganizationKey();
        var sharedCount = await SeedOrganizationItems(
            org.Id,
            orgKey,
            cryptoService,
            sharedLoginCount: 10,
            sharedNoteCount: 5);
            
        totalItems += sharedCount;
        
        return totalItems;
    }
}