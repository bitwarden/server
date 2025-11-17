using Bit.Core.Entities;

namespace Bit.Core.KeyManagement.Queries.Interfaces;

public interface IIsV2EncryptionUserQuery
{
    Task<bool> Run(User user);
}


