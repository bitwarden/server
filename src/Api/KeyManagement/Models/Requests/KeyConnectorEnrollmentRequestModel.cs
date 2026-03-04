using Bit.Core.Utilities;

namespace Bit.Api.KeyManagement.Models.Requests;

public class KeyConnectorEnrollmentRequestModel
{
    [EncryptedString]
    public required string KeyConnectorKeyWrappedUserKey { get; set; }
}
