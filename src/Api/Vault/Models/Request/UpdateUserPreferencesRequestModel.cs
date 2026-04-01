using Bit.Core.Utilities;

namespace Bit.Api.Vault.Models.Request;

public class UpdateUserPreferencesRequestModel
{
    /// <summary>
    /// The encrypted preference's data.
    /// </summary>
    [EncryptedString]
    [EncryptedStringLength(10000)]
    public required string Data { get; set; }
}
