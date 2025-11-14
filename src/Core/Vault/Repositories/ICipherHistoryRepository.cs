using System.Collections.Generic;
using Bit.Core.Repositories;
using Bit.Core.Vault.Entities;

namespace Bit.Core.Vault.Repositories;

public interface ICipherHistoryRepository : IRepository<CipherHistory, Guid>
{
    Task<ICollection<CipherHistory>> GetManyByCipherIdAsync(Guid cipherId);
}
