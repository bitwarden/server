using Bit.Core.Entities;
using Bit.Core.KeyManagement.Queries.Interfaces;

namespace Bit.Core.KeyManagement.Queries;

public class GetMinimumClientVersionForUserQuery()
    : IGetMinimumClientVersionForUserQuery
{
    public Task<Version?> Run(User? user)
    {
        if (user == null)
        {
            return Task.FromResult<Version?>(null);
        }

        if (user.IsSecurityVersionTwo())
        {
            return Task.FromResult(Constants.MinimumClientVersionForV2Encryption)!;
        }

        return Task.FromResult<Version?>(null);
    }
}
