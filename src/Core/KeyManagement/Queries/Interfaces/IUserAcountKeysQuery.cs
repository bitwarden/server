#nullable enable

using Bit.Core.Entities;
using Bit.Core.KeyManagement.Models.Data.Models;

namespace Bit.Api.KeyManagement.Queries.Interfaces;

public interface IUserAccountKeysQuery
{
    Task<UserAccountKeysData> Run(User user);
}
