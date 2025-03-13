using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Bit.Core.Entities;
using Bit.Core.Vault.Entities;

namespace Bit.Seeder.Services;

public class DatabaseService : IDatabaseService
{
    private readonly DatabaseContext _dbContext;
    private readonly ILogger<DatabaseService> _logger;

    public DatabaseService(DatabaseContext dbContext, ILogger<DatabaseService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task ClearDatabaseAsync()
    {
        _logger.LogInformation("Clearing database...");

        var ciphers = await _dbContext.Ciphers.ToListAsync();
        _dbContext.Ciphers.RemoveRange(ciphers);

        var users = await _dbContext.Users.ToListAsync();
        _dbContext.Users.RemoveRange(users);

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Database cleared successfully.");
    }

    public async Task SaveUsersAsync(IEnumerable<User> users)
    {
        _logger.LogInformation("Saving users to database...");

        foreach (var user in users)
        {
            await _dbContext.Users.AddAsync(user);
        }

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation($"Successfully saved {users.Count()} users to database.");
    }

    public async Task SaveCiphersAsync(IEnumerable<Cipher> ciphers)
    {
        _logger.LogInformation("Saving ciphers to database...");

        foreach (var cipher in ciphers)
        {
            await _dbContext.Ciphers.AddAsync(cipher);
        }

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation($"Successfully saved {ciphers.Count()} ciphers to database.");
    }

    public async Task<List<User>> GetUsersAsync()
    {
        _logger.LogInformation("Retrieving all users from database...");
        var users = await _dbContext.Users.ToListAsync();
        _logger.LogInformation($"Successfully retrieved {users.Count} users from database.");
        return users;
    }

    public async Task<List<Cipher>> GetCiphersAsync()
    {
        _logger.LogInformation("Retrieving all ciphers from database...");
        var ciphers = await _dbContext.Ciphers.ToListAsync();
        _logger.LogInformation($"Successfully retrieved {ciphers.Count} ciphers from database.");
        return ciphers;
    }

    public async Task<List<Cipher>> GetCiphersByUserIdAsync(Guid userId)
    {
        _logger.LogInformation($"Retrieving ciphers for user {userId} from database...");
        var ciphers = await _dbContext.Ciphers.Where(c => c.UserId == userId).ToListAsync();
        _logger.LogInformation($"Successfully retrieved {ciphers.Count} ciphers for user {userId}.");
        return ciphers;
    }
}
