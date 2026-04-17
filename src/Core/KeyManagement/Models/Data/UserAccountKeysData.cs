namespace Bit.Core.KeyManagement.Models.Data;

/// <summary>
/// Represents an expanded account cryptographic state for a user. Expanded here means
/// that it does not only contain the (wrapped) private / signing key, but also the public
/// key / verifying key. The client side only needs a subset of this data to unlock
/// their vault and the public parts can be derived.
/// </summary>
public class UserAccountKeysData
{
    public required PublicKeyEncryptionKeyPairData PublicKeyEncryptionKeyPairData { get; set; }
    public SignatureKeyPairData? SignatureKeyPairData { get; set; }
    public SecurityStateData? SecurityStateData { get; set; }

    /// <summary>
    /// Checks whether the account cryptographic state is for a V1 encryption user or a V2 encryption user.
    /// Throws if the state is invalid
    /// </summary>
    public bool IsV2Encryption()
    {
        if (PublicKeyEncryptionKeyPairData.SignedPublicKey != null && SignatureKeyPairData != null && SecurityStateData != null)
        {
            return true;
        }
        else if (PublicKeyEncryptionKeyPairData.SignedPublicKey == null && SignatureKeyPairData == null && SecurityStateData == null)
        {
            return false;
        }
        else
        {
            throw new InvalidOperationException("Invalid account cryptographic state: V2 encryption fields must be either all present or all absent.");
        }
    }
}
