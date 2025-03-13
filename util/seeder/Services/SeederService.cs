using System.Text.Json;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bogus;
using Microsoft.Extensions.Logging;

namespace Bit.Seeder.Services;

public class SeederService : ISeederService
{
    private readonly IEncryptionService _encryptionService;
    private readonly IDatabaseService _databaseService;
    private readonly ILogger<SeederService> _logger;
    private readonly Faker _faker;
    private readonly string _defaultPassword = "password";

    public SeederService(
        IEncryptionService encryptionService,
        IDatabaseService databaseService,
        ILogger<SeederService> logger)
    {
        _encryptionService = encryptionService;
        _databaseService = databaseService;
        _logger = logger;
        _faker = new Faker();
        
        // Set the random seed to ensure reproducible data
        Randomizer.Seed = new Random(42);
    }

    public async Task GenerateSeedsAsync(int userCount, int ciphersPerUser, string outputName)
    {
        _logger.LogInformation("Generating seeds: {UserCount} users with {CiphersPerUser} ciphers each", userCount, ciphersPerUser);
        
        // Create timestamped folder under a named folder in seeds directory
        var seedsBaseDir = Path.Combine(Directory.GetCurrentDirectory(), "seeds");
        Directory.CreateDirectory(seedsBaseDir);
        
        var namedDir = Path.Combine(seedsBaseDir, outputName);
        Directory.CreateDirectory(namedDir);
        
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var outputDir = Path.Combine(namedDir, timestamp);
        Directory.CreateDirectory(outputDir);
        
        // Create users and ciphers subdirectories
        Directory.CreateDirectory(Path.Combine(outputDir, "users"));
        Directory.CreateDirectory(Path.Combine(outputDir, "ciphers"));
        
        _logger.LogInformation("Seed output directory: {OutputDir}", outputDir);
        
        // Generate users
        var users = GenerateUsers(userCount);
        
        // Generate ciphers for each user
        var allCiphers = new List<Cipher>();
        foreach (var user in users)
        {
            var ciphers = GenerateCiphers(user, ciphersPerUser);
            allCiphers.AddRange(ciphers);
            
            // Save each user's ciphers to a file
            var cipherFilePath = Path.Combine(outputDir, "ciphers", $"{user.Id}.json");
            await File.WriteAllTextAsync(cipherFilePath, JsonSerializer.Serialize(ciphers, new JsonSerializerOptions
            {
                WriteIndented = true
            }));
        }
        
        // Save users to a file
        var userFilePath = Path.Combine(outputDir, "users", "users.json");
        await File.WriteAllTextAsync(userFilePath, JsonSerializer.Serialize(users, new JsonSerializerOptions
        {
            WriteIndented = true
        }));
        
        _logger.LogInformation("Successfully generated {UserCount} users and {CipherCount} ciphers", users.Count, allCiphers.Count);
        _logger.LogInformation("Seed data saved to directory: {OutputDir}", outputDir);
    }

    public async Task GenerateAndLoadSeedsAsync(int userCount, int ciphersPerUser, string seedName)
    {
        _logger.LogInformation("Generating and loading seeds directly: {UserCount} users with {CiphersPerUser} ciphers each", 
            userCount, ciphersPerUser);
        
        // Generate users directly without saving to files
        var users = GenerateUsers(userCount);
        
        // Clear the database first
        await _databaseService.ClearDatabaseAsync();
        
        // Save users to database
        await _databaseService.SaveUsersAsync(users);
        _logger.LogInformation("Saved {UserCount} users directly to database", users.Count);
        
        // Generate and save ciphers for each user
        int totalCiphers = 0;
        foreach (var user in users)
        {
            var ciphers = GenerateCiphers(user, ciphersPerUser);
            await _databaseService.SaveCiphersAsync(ciphers);
            totalCiphers += ciphers.Count;
            _logger.LogInformation("Saved {CipherCount} ciphers for user {UserId} directly to database", 
                ciphers.Count, user.Id);
        }
        
        _logger.LogInformation("Successfully generated and loaded {UserCount} users and {CipherCount} ciphers directly to database", 
            users.Count, totalCiphers);
    }

    public async Task LoadSeedsAsync(string seedName, string? timestamp = null)
    {
        // Construct path to seeds directory
        var seedsBaseDir = Path.Combine(Directory.GetCurrentDirectory(), "seeds");
        var namedDir = Path.Combine(seedsBaseDir, seedName);
        
        if (!Directory.Exists(namedDir))
        {
            _logger.LogError("Seed directory not found: {SeedDir}", namedDir);
            return;
        }
        
        string seedDir;
        
        // If timestamp is specified, use that exact directory
        if (!string.IsNullOrEmpty(timestamp))
        {
            seedDir = Path.Combine(namedDir, timestamp);
            if (!Directory.Exists(seedDir))
            {
                _logger.LogError("Timestamp directory not found: {TimestampDir}", seedDir);
                return;
            }
        }
        else
        {
            // Otherwise, find the most recent timestamped directory
            var timestampDirs = Directory.GetDirectories(namedDir);
            if (timestampDirs.Length == 0)
            {
                _logger.LogError("No seed data found in directory: {SeedDir}", namedDir);
                return;
            }
            
            // Sort by directory name (which is a timestamp) in descending order
            Array.Sort(timestampDirs);
            Array.Reverse(timestampDirs);
            
            // Use the most recent one
            seedDir = timestampDirs[0];
            _logger.LogInformation("Using most recent seed data from: {SeedDir}", seedDir);
        }
        
        _logger.LogInformation("Loading seeds from directory: {SeedDir}", seedDir);
        
        // Clear database first
        await _databaseService.ClearDatabaseAsync();
        
        // Load users
        var userFilePath = Path.Combine(seedDir, "users", "users.json");
        if (!File.Exists(userFilePath))
        {
            _logger.LogError("User file not found: {UserFilePath}", userFilePath);
            return;
        }
        
        var userJson = await File.ReadAllTextAsync(userFilePath);
        var users = JsonSerializer.Deserialize<List<User>>(userJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new List<User>();
        
        if (users.Count == 0)
        {
            _logger.LogError("No users found in user file");
            return;
        }
        
        // Save users to database
        await _databaseService.SaveUsersAsync(users);
        
        // Load and save ciphers for each user
        var cipherDir = Path.Combine(seedDir, "ciphers");
        if (!Directory.Exists(cipherDir))
        {
            _logger.LogError("Cipher directory not found: {CipherDir}", cipherDir);
            return;
        }
        
        var cipherFiles = Directory.GetFiles(cipherDir, "*.json");
        foreach (var cipherFile in cipherFiles)
        {
            var cipherJson = await File.ReadAllTextAsync(cipherFile);
            var ciphers = JsonSerializer.Deserialize<List<Cipher>>(cipherJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new List<Cipher>();
            
            if (ciphers.Count > 0)
            {
                await _databaseService.SaveCiphersAsync(ciphers);
            }
        }
        
        _logger.LogInformation("Successfully loaded seed data into database");
    }
    
    public async Task ExtractSeedsAsync(string seedName)
    {
        _logger.LogInformation("Extracting seed data from database for seed name: {SeedName}", seedName);
        
        // Create timestamped folder under a named folder in seeds directory
        var seedsBaseDir = Path.Combine(Directory.GetCurrentDirectory(), "seeds");
        Directory.CreateDirectory(seedsBaseDir);
        
        var namedDir = Path.Combine(seedsBaseDir, seedName);
        Directory.CreateDirectory(namedDir);
        
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var outputDir = Path.Combine(namedDir, timestamp);
        Directory.CreateDirectory(outputDir);
        
        // Create users and ciphers subdirectories
        Directory.CreateDirectory(Path.Combine(outputDir, "users"));
        Directory.CreateDirectory(Path.Combine(outputDir, "ciphers"));

        _logger.LogInformation("Seed output directory: {OutputDir}", outputDir);
        
        try
        {
            // Get all users from the database
            var users = await _databaseService.GetUsersAsync();
            if (users == null || users.Count == 0)
            {
                _logger.LogWarning("No users found in the database");
                return;
            }
            
            _logger.LogInformation("Extracted {Count} users from database", users.Count);
            
            // Save users to a file
            var userFilePath = Path.Combine(outputDir, "users", "users.json");
            await File.WriteAllTextAsync(userFilePath, JsonSerializer.Serialize(users, new JsonSerializerOptions
            {
                WriteIndented = true
            }));
            
            int totalCiphers = 0;
            // Get ciphers for each user
            foreach (var user in users)
            {
                var ciphers = await _databaseService.GetCiphersByUserIdAsync(user.Id);
                if (ciphers != null && ciphers.Count > 0)
                {
                    // Save ciphers to a file
                    var cipherFilePath = Path.Combine(outputDir, "ciphers", $"{user.Id}.json");
                    await File.WriteAllTextAsync(cipherFilePath, JsonSerializer.Serialize(ciphers, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    }));
                    totalCiphers += ciphers.Count;
                }
            }
            
            _logger.LogInformation("Successfully extracted {UserCount} users and {CipherCount} ciphers", users.Count, totalCiphers);
            _logger.LogInformation("Seed data saved to directory: {OutputDir}", outputDir);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting seed data: {Message}", ex.Message);
            throw;
        }
    }
    
    private List<User> GenerateUsers(int count)
    {
        _logger.LogInformation("Generating {Count} users", count);
        
        var users = new List<User>();
        
        for (int i = 0; i < count; i++)
        {
            var userId = Guid.NewGuid();
            var email = _faker.Internet.Email(provider: "example.com");
            var name = _faker.Name.FullName();
            var masterPassword = _encryptionService.HashPassword(_defaultPassword);
            var masterPasswordHint = "It's the word 'password'";
            var key = _encryptionService.DeriveKey(_defaultPassword, email);
            
            var user = new User
            {
                Id = userId,
                Email = email,
                Name = name,
                MasterPassword = masterPassword,
                MasterPasswordHint = masterPasswordHint,
                SecurityStamp = Guid.NewGuid().ToString(),
                EmailVerified = true,
                ApiKey = Guid.NewGuid().ToString("N").Substring(0, 30),
                Kdf = KdfType.PBKDF2_SHA256,
                KdfIterations = 100000,
                CreationDate = DateTime.UtcNow,
                RevisionDate = DateTime.UtcNow,
                Key = _encryptionService.EncryptString(Convert.ToBase64String(key), key)
            };
            
            users.Add(user);
        }
        
        return users;
    }
    
    private List<Cipher> GenerateCiphers(User user, int count)
    {
        _logger.LogInformation("Generating {Count} ciphers for user {UserId}", count, user.Id);
        
        var ciphers = new List<Cipher>();
        var key = _encryptionService.DeriveKey(_defaultPassword, user.Email);
        
        for (int i = 0; i < count; i++)
        {
            var cipherId = Guid.NewGuid();
            CipherType type;
            string name;
            string? notes = null;
            
            var typeRandom = _faker.Random.Int(1, 4);
            type = (CipherType)typeRandom;
            
            switch (type)
            {
                case CipherType.Login:
                    name = $"Login - {_faker.Internet.DomainName()}";
                    var loginData = new 
                    {
                        Name = name,
                        Notes = notes,
                        Username = _faker.Internet.UserName(),
                        Password = _faker.Internet.Password(),
                        Uris = new[]
                        {
                            new { Uri = $"https://{_faker.Internet.DomainName()}" }
                        }
                    };
                    
                    var loginDataJson = JsonSerializer.Serialize(loginData);
                    
                    ciphers.Add(new Cipher
                    {
                        Id = cipherId,
                        UserId = user.Id,
                        Type = type,
                        Data = _encryptionService.EncryptString(loginDataJson, key),
                        CreationDate = DateTime.UtcNow,
                        RevisionDate = DateTime.UtcNow,
                        Reprompt = CipherRepromptType.None
                    });
                    break;
                    
                case CipherType.SecureNote:
                    name = $"Note - {_faker.Lorem.Word()}";
                    notes = _faker.Lorem.Paragraph();
                    var secureNoteData = new 
                    {
                        Name = name,
                        Notes = notes,
                        Type = 0 // Text
                    };
                    
                    var secureNoteDataJson = JsonSerializer.Serialize(secureNoteData);
                    
                    ciphers.Add(new Cipher
                    {
                        Id = cipherId,
                        UserId = user.Id,
                        Type = type,
                        Data = _encryptionService.EncryptString(secureNoteDataJson, key),
                        CreationDate = DateTime.UtcNow,
                        RevisionDate = DateTime.UtcNow,
                        Reprompt = CipherRepromptType.None
                    });
                    break;
                    
                case CipherType.Card:
                    name = $"Card - {_faker.Finance.CreditCardNumber().Substring(0, 4)}";
                    var cardData = new 
                    {
                        Name = name,
                        Notes = notes,
                        CardholderName = _faker.Name.FullName(),
                        Number = _faker.Finance.CreditCardNumber(),
                        ExpMonth = _faker.Random.Int(1, 12).ToString(),
                        ExpYear = _faker.Random.Int(DateTime.UtcNow.Year, DateTime.UtcNow.Year + 10).ToString(),
                        Code = _faker.Random.Int(100, 999).ToString()
                    };
                    
                    var cardDataJson = JsonSerializer.Serialize(cardData);
                    
                    ciphers.Add(new Cipher
                    {
                        Id = cipherId,
                        UserId = user.Id,
                        Type = type,
                        Data = _encryptionService.EncryptString(cardDataJson, key),
                        CreationDate = DateTime.UtcNow,
                        RevisionDate = DateTime.UtcNow,
                        Reprompt = CipherRepromptType.None
                    });
                    break;
                    
                case CipherType.Identity:
                    name = $"Identity - {_faker.Name.FullName()}";
                    var identityData = new 
                    {
                        Name = name,
                        Notes = notes,
                        Title = _faker.Name.Prefix(),
                        FirstName = _faker.Name.FirstName(),
                        MiddleName = _faker.Name.FirstName(),
                        LastName = _faker.Name.LastName(),
                        Email = _faker.Internet.Email(),
                        Phone = _faker.Phone.PhoneNumber(),
                        Address1 = _faker.Address.StreetAddress(),
                        City = _faker.Address.City(),
                        State = _faker.Address.State(),
                        PostalCode = _faker.Address.ZipCode(),
                        Country = _faker.Address.CountryCode()
                    };
                    
                    var identityDataJson = JsonSerializer.Serialize(identityData);
                    
                    ciphers.Add(new Cipher
                    {
                        Id = cipherId,
                        UserId = user.Id,
                        Type = type,
                        Data = _encryptionService.EncryptString(identityDataJson, key),
                        CreationDate = DateTime.UtcNow,
                        RevisionDate = DateTime.UtcNow,
                        Reprompt = CipherRepromptType.None
                    });
                    break;
            }
        }
        
        return ciphers;
    }
} 