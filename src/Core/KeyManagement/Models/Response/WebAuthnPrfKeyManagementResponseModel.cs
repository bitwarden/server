namespace Bit.Core.KeyManagement.Models.Response;

/// <summary>
/// Response model for WebAuthn PRF decryption options used in key management contexts.
/// This mirrors WebAuthnPrfDecryptionOption from Auth namespace but belongs to KeyManagement.
/// </summary>
public class WebAuthnPrfKeyManagementResponseModel
{
    public string EncryptedPrivateKey { get; }
    public string EncryptedUserKey { get; }
    public string CredentialId { get; }
    public string[] Transports { get; }

    public WebAuthnPrfKeyManagementResponseModel(
        string encryptedPrivateKey,
        string encryptedUserKey,
        string credentialId,
        string[] transports)
    {
        EncryptedPrivateKey = encryptedPrivateKey;
        EncryptedUserKey = encryptedUserKey;
        CredentialId = credentialId;
        Transports = transports;
    }
}