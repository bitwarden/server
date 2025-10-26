using Bit.Core.Repositories;
using Bit.Core.Vault.Entities;

namespace Bit.Core.Vault.Repositories;

public interface ICipherHistoryRepository : IRepository<CipherHistory, Guid>
{
}
