using Bit.Core.Entities;
using Bit.Core.KeyManagement.UserKey.Models.Data;

namespace Bit.Core.KeyManagement.UserKey.Queries.Interfaces;

public interface IKeyRotationDataQuery
{
    Task<KeyRotationData> Run(User user);
}
