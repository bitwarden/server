namespace Bit.Core.KeyManagement.Models.Api.Response;

public class UserDecryptionResponseModel
{
    /// <summary>
    /// Returns the unlock data when the user has a master password that can be used to decrypt their vault.
    /// </summary>
    public MasterPasswordUnlockResponseModel? MasterPasswordUnlock { get; set; }
}
