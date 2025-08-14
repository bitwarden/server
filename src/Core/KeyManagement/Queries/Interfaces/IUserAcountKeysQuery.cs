#nullable enable

using Bit.Core.Entities;
using Bit.Core.KeyManagement.Models.Data;

namespace Bit.Core.KeyManagement.Queries.Interfaces;

public interface IUserAccountKeysQuery
{
    Task<UserAccountKeysData> Run(User user);
}
