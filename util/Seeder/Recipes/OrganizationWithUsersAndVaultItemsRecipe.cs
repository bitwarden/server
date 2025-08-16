#nullable enable

using Bit.Core.Enums;
using Bit.Core.Utilities;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models;
using Bit.Infrastructure.EntityFramework.Models;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Infrastructure.EntityFramework.Vault.Models;
using Bit.Seeder.Factories;
using Bit.Seeder.Services;
using LinqToDB.Data;
using LinqToDB.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

namespace Bit.Seeder.Recipes;

/// <summary>
/// Recipe for creating an organization with multiple users, each with their own vault items
/// </summary>
public class OrganizationWithUsersAndVaultItemsRecipe(DatabaseContext db)
{
    /// <summary>
    /// Creates a complete organization with users and vault items
    /// </summary>
    public async Task<(Organization org, int userCount, int totalItemCount)> CreateOrganizationWithUsersAndItems(
        string organizationName,
        string emailDomain,
        string defaultPassword,
        ISeederCryptoService cryptoService,
        IDataProtectionService dataProtection,
        IPasswordHasher<User> passwordHasher,
        int userCount = 100,
        int minItemsPerUser = 3,
        int maxItemsPerUser = 5)
    {
        Console.WriteLine($"Creating organization '{organizationName}' with {userCount} users...");

        // Step 1: Create the organization
        var orgKey = cryptoService.GenerateOrganizationKey();
        var (orgPublicKey, orgPrivateKey) = cryptoService.GenerateUserKeyPair();

        var org = new Organization
        {
            Id = CoreHelpers.GenerateComb(),
            Name = organizationName,
            BusinessName = $"{organizationName} Inc.",
            BillingEmail = $"billing@{emailDomain}",
            Plan = "Enterprise (Annually)",
            PlanType = Bit.Core.Billing.Enums.PlanType.EnterpriseAnnually,
            Seats = userCount + 50, // Extra seats for growth
            MaxCollections = short.MaxValue,
            UseGroups = true,
            UseDirectory = true,
            UseEvents = true,
            UseTotp = true,
            Use2fa = true,
            UseApi = true,
            UseSso = true,
            SelfHost = false,
            UsersGetPremium = true,
            UseCustomPermissions = true,
            UseScim = true,
            UseResetPassword = true,
            UsePasswordManager = true,
            UseSecretsManager = false,
            SmSeats = null,
            SmServiceAccounts = null,
            LimitCollectionCreation = false,
            LimitCollectionDeletion = false,
            Storage = null,
            MaxStorageGb = null,
            Status = OrganizationStatusType.Created,
            Enabled = true,
            LicenseKey = null,
            PublicKey = orgPublicKey,
            PrivateKey = dataProtection.Protect(cryptoService.EncryptPrivateKey(orgPrivateKey, orgKey)),
            TwoFactorProviders = null,
            ExpirationDate = null,
            Gateway = null,
            GatewayCustomerId = null,
            GatewaySubscriptionId = null,
            ReferenceData = null
        };

        db.Organizations.Add(org);
        await db.SaveChangesAsync();
        Console.WriteLine($"✅ Created organization: {org.Name}");

        // Create organization API keys
        var apiKeys = new[]
        {
            new OrganizationApiKey
            {
                Id = CoreHelpers.GenerateComb(),
                OrganizationId = org.Id,
                Type = OrganizationApiKeyType.Default,
                ApiKey = CoreHelpers.SecureRandomString(30),
                RevisionDate = DateTime.UtcNow
            },
            new OrganizationApiKey
            {
                Id = CoreHelpers.GenerateComb(),
                OrganizationId = org.Id,
                Type = OrganizationApiKeyType.BillingSync,
                ApiKey = CoreHelpers.SecureRandomString(30),
                RevisionDate = DateTime.UtcNow
            }
        };

        db.OrganizationApiKeys.AddRange(apiKeys);
        await db.SaveChangesAsync();
        Console.WriteLine($"✅ Created organization API keys");

        // Step 2: Create owner user
        var ownerEmail = $"owner@{emailDomain}";
        var ownerUserKey = cryptoService.GenerateUserKey();
        var owner = UserSeeder.CreateUser(ownerEmail, defaultPassword, cryptoService, dataProtection, passwordHasher, ownerUserKey);

        db.Users.Add(owner);
        await db.SaveChangesAsync();

        // Make owner an organization owner
        var ownerOrgUser = new OrganizationUser
        {
            Id = CoreHelpers.GenerateComb(),
            OrganizationId = org.Id,
            UserId = owner.Id,
            Email = owner.Email,
            Key = RsaEncrypt(orgKey, owner.PublicKey ?? throw new InvalidOperationException("Owner missing public key")),
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.Owner,
            ExternalId = null
        };

        db.OrganizationUsers.Add(ownerOrgUser);
        await db.SaveChangesAsync();
        Console.WriteLine($"✅ Created organization owner: {ownerEmail}");

        // Step 3: Create users with vault items
        var users = new List<User>();
        var orgUsers = new List<OrganizationUser>();
        var allCiphers = new List<Cipher>();
        var random = new Random();

        for (int i = 1; i <= userCount; i++)
        {
            var email = $"user{i:D3}@{emailDomain}"; // user001@domain.com format

            // Generate user key for this user
            var userKey = cryptoService.GenerateUserKey();

            // Create user with their key
            var user = UserSeeder.CreateUser(email, defaultPassword, cryptoService, dataProtection, passwordHasher, userKey);
            users.Add(user);

            // Create organization user
            var orgUser = new OrganizationUser
            {
                Id = CoreHelpers.GenerateComb(),
                OrganizationId = org.Id,
                UserId = user.Id,
                Email = user.Email,
                Key = RsaEncrypt(orgKey, user.PublicKey ?? throw new InvalidOperationException($"User {user.Email} missing public key")),
                Status = OrganizationUserStatusType.Confirmed,
                Type = OrganizationUserType.User,
                ExternalId = null
            };
            orgUsers.Add(orgUser);

            // Create vault items for this user
            var itemCount = random.Next(minItemsPerUser, maxItemsPerUser + 1);
            var userCiphers = CreateUserVaultItems(user.Id, userKey, cryptoService, itemCount);
            allCiphers.AddRange(userCiphers);

            if (i % 10 == 0)
            {
                Console.WriteLine($"   Created {i}/{userCount} users...");
            }
        }

        // Bulk insert in batches to avoid timeouts
        const int batchSize = 20;
        var bulkOptions = new BulkCopyOptions
        {
            BulkCopyTimeout = 120 // 2 minutes timeout per batch
        };

        // Insert users in batches
        for (int i = 0; i < users.Count; i += batchSize)
        {
            var batch = users.Skip(i).Take(batchSize).ToList();
            await db.BulkCopyAsync(bulkOptions, batch);
            Console.WriteLine($"   Saved users {i + 1}-{Math.Min(i + batchSize, users.Count)}");
        }
        Console.WriteLine($"✅ Created {users.Count} users");

        // Insert organization users in batches
        for (int i = 0; i < orgUsers.Count; i += batchSize)
        {
            var batch = orgUsers.Skip(i).Take(batchSize).ToList();
            await db.BulkCopyAsync(bulkOptions, batch);
        }
        Console.WriteLine($"✅ Added users to organization");

        // Insert vault items in batches (larger batches since they're simpler)
        const int cipherBatchSize = 100;
        for (int i = 0; i < allCiphers.Count; i += cipherBatchSize)
        {
            var batch = allCiphers.Skip(i).Take(cipherBatchSize).ToList();
            await db.BulkCopyAsync(bulkOptions, batch);
            Console.WriteLine($"   Saved vault items {i + 1}-{Math.Min(i + cipherBatchSize, allCiphers.Count)}");
        }
        Console.WriteLine($"✅ Created {allCiphers.Count} vault items");

        // Step 4: Create some shared organizational items
        var sharedCiphers = CreateOrganizationVaultItems(org.Id, orgKey, cryptoService, 10);
        await db.BulkCopyAsync(bulkOptions, sharedCiphers);
        Console.WriteLine($"✅ Created {sharedCiphers.Count} shared organizational items");

        // Summary
        var totalUsers = users.Count + 1; // +1 for owner
        var totalItems = allCiphers.Count + sharedCiphers.Count;

        Console.WriteLine("\n=== Organization Creation Summary ===");
        Console.WriteLine($"Organization: {org.Name}");
        Console.WriteLine($"Total Users: {totalUsers}");
        Console.WriteLine($"Total Vault Items: {totalItems}");
        Console.WriteLine($"  - Personal Items: {allCiphers.Count}");
        Console.WriteLine($"  - Shared Items: {sharedCiphers.Count}");
        Console.WriteLine($"Default Password: {defaultPassword}");
        Console.WriteLine($"Email Pattern: user###@{emailDomain}");
        Console.WriteLine($"Owner: owner@{emailDomain}");
        Console.WriteLine("=====================================");

        return (org, totalUsers, totalItems);
    }

    private List<Cipher> CreateUserVaultItems(Guid userId, byte[] userKey, ISeederCryptoService cryptoService, int count)
    {
        var ciphers = new List<Cipher>();
        var random = new Random();

        // Get random logins
        var logins = CipherSeeder.SampleData.Logins
            .OrderBy(_ => random.Next())
            .Take(count)
            .ToList();

        foreach (var (name, username, uri, password) in logins)
        {
            var cipher = CipherSeeder.CreateLogin(
                name, username, password, uri,
                userId, null, userKey, cryptoService,
                notes: $"Personal login for {name}");
            ciphers.Add(cipher);
        }

        // Add a secure note if we have room
        if (count > 3)
        {
            var note = CipherSeeder.SampleData.SecureNotes.OrderBy(_ => random.Next()).First();
            var noteCipher = CipherSeeder.CreateSecureNote(
                note.name, note.content,
                userId, null, userKey, cryptoService);
            ciphers.Add(noteCipher);
        }

        return ciphers;
    }

    private List<Cipher> CreateOrganizationVaultItems(Guid orgId, byte[] orgKey, ISeederCryptoService cryptoService, int count)
    {
        var ciphers = new List<Cipher>();

        // Common organizational logins
        var orgLogins = new[]
        {
            ("Company Email", "admin", "https://mail.company.com", "CompanyMail2024!"),
            ("HR Portal", "hr-admin", "https://hr.company.com", "HRAccess#2024"),
            ("Timesheet System", "timesheet", "https://time.company.com", "TimeTrack@2024"),
            ("Project Management", "pm-admin", "https://projects.company.com", "ProjectHub!24"),
            ("Company VPN", "vpn-user", "vpn.company.com", "SecureVPN#2024"),
            ("Slack Workspace", "admin@company.com", "https://company.slack.com", "SlackTeam2024!"),
            ("GitHub Organization", "company-admin", "https://github.com/company", "GitOrg#2024"),
            ("AWS Console", "aws-admin", "https://aws.amazon.com", "CloudAdmin2024!"),
            ("Office WiFi", "CompanyWiFi", "WiFi Network", "WiFiPass#2024"),
            ("Building Access", "1234", "Front Door", "BuildingCode2024")
        };

        foreach (var (name, username, uri, password) in orgLogins.Take(count))
        {
            var cipher = CipherSeeder.CreateLogin(
                $"[Company] {name}", username, password, uri,
                null, orgId, orgKey, cryptoService,
                notes: $"Shared company resource - {name}");
            ciphers.Add(cipher);
        }

        return ciphers;
    }

    private static string RsaEncrypt(byte[] data, string publicKeyBase64)
    {
        // Type 4 format for RSA encryption: "4.base64data"
        using var rsa = System.Security.Cryptography.RSA.Create();
        var publicKeyBytes = Convert.FromBase64String(publicKeyBase64);
        rsa.ImportSubjectPublicKeyInfo(publicKeyBytes, out _);

        var encrypted = rsa.Encrypt(data, System.Security.Cryptography.RSAEncryptionPadding.OaepSHA1);
        return $"4.{Convert.ToBase64String(encrypted)}";
    }
}
