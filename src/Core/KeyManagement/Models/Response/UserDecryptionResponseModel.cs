using System.Text.Json.Serialization;

namespace Bit.Core.KeyManagement.Models.Response;

public class UserDecryptionResponseModel
{
    /// <summary>
    /// Returns the unlock data when the user has a master password that can be used to decrypt their vault.
    /// </summary>
    public MasterPasswordUnlockResponseModel? MasterPasswordUnlock { get; set; }

    /// <summary>
    /// Gets or sets the WebAuthn PRF decryption keys.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public WebAuthnPrfKeyManagementResponseModel[]? WebAuthnPrfOptions { get; set; }
}
