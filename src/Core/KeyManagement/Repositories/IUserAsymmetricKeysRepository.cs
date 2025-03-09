#nullable enable
using Bit.Core.KeyManagement.Models.Data;

namespace Bit.Core.KeyManagement.Repositories;

public interface IUserAsymmetricKeysRepository
{
    Task RegenerateUserAsymmetricKeysAsync(UserAsymmetricKeys userAsymmetricKeys);
}
