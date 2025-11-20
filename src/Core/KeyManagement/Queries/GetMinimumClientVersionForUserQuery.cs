using Bit.Core.Entities;
using Bit.Core.KeyManagement.Queries.Interfaces;

namespace Bit.Core.KeyManagement.Queries;

public class GetMinimumClientVersionForUserQuery(IIsV2EncryptionUserQuery isV2EncryptionUserQuery)
    : IGetMinimumClientVersionForUserQuery
{
    public async Task<Version?> Run(User? user)
    {
        if (user == null)
        {
            return null;
        }

        if (await isV2EncryptionUserQuery.Run(user))
        {
            return Constants.MinimumClientVersion;
        }

        return null;
    }
}
