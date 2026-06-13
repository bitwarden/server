#nullable enable
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Repositories;

namespace Bit.Core.KeyManagement.Repositories;

public interface IUserAsymmetricKeysRepository
{
    Task RegenerateUserAsymmetricKeysAsync(UserAsymmetricKeys userAsymmetricKeys,
        IEnumerable<DatabaseTransactionAction> updateDataActions);
}
