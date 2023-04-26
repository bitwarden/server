using Bit.Core.Auth.Entities;
using Bit.Core.Repositories;

namespace Bit.Core.Auth.Repositories;

public interface IWebAuthnRepository : IRepository<WebAuthnCredential, Guid>
{
    Task<ICollection<WebAuthnCredential>> GetManyByUserIdAsync(Guid userId);
}
