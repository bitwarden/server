using Bit.Core.KeyManagement.Models.Api.Request;

namespace Bit.Core.KeyManagement.Models.Data;

public class KeyConnectorKeysData
{
    public required string KeyConnectorKeyWrappedUserKey { get; set; }

    public required AccountKeysRequestModel AccountKeys { get; set; }

    public required string OrgIdentifier { get; init; }
}
