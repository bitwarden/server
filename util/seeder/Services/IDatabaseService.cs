using Bit.Core.Entities;
using Bit.Core.Vault.Entities;

namespace Bit.Seeder.Services;

public interface IDatabaseService
{
    Task ClearDatabaseAsync();
    Task SaveUsersAsync(IEnumerable<User> users);
    Task SaveCiphersAsync(IEnumerable<Cipher> ciphers);
    Task<List<User>> GetUsersAsync();
    Task<List<Cipher>> GetCiphersAsync();
    Task<List<Cipher>> GetCiphersByUserIdAsync(Guid userId);
}
