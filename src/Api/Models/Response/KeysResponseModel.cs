using Bit.Core.KeyManagement.Models.Api.Response;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Models.Api;

namespace Bit.Api.Models.Response;

public class KeysResponseModel : ResponseModel
{
    public KeysResponseModel(UserAccountKeysData accountKeys, string? masterKeyWrappedUserKey)
        : base("keys")
    {
        if (masterKeyWrappedUserKey != null)
        {
            Key = masterKeyWrappedUserKey;
        }

        PublicKey = accountKeys.PublicKeyEncryptionKeyPairData.PublicKey;
        PrivateKey = accountKeys.PublicKeyEncryptionKeyPairData.WrappedPrivateKey;
        AccountKeys = new PrivateKeysResponseModel(accountKeys);
    }

    /// <summary>
    /// The master key wrapped user key. The master key can either be a master-password master key or a
    /// key-connector master key.
    /// </summary>
    public string? Key { get; set; }
    [Obsolete("Use AccountKeys.PublicKeyEncryptionKeyPair.PublicKey instead")]
    public string PublicKey { get; set; }
    [Obsolete("Use AccountKeys.PublicKeyEncryptionKeyPair.WrappedPrivateKey instead")]
    public string PrivateKey { get; set; }
    public PrivateKeysResponseModel AccountKeys { get; set; }
}
