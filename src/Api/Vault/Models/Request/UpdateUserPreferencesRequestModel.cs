using Bit.Core.Utilities;

namespace Bit.Api.Vault.Models.Request;

public class UpdateUserPreferencesRequestModel
{
    [EncryptedString]
    [EncryptedStringLength(10000)]
    public required string Data { get; set; }
}
