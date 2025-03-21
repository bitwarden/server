using Bit.Core.Entities;
using Bit.Core.Utilities;

namespace Bit.Core.Auth.Entities;

public class OpaqueKeyExchangeCredential : ITableObject<Guid>
{
    /// <summary>
    /// Identity column
    /// </summary>
    public Guid Id { get; set; }
    /// <summary>
    /// User who owns the credential
    /// </summary>
    public Guid UserId { get; set; }
    /// <summary>
    /// This describes the cipher configuration that both the server and client know.
    /// This is returned on the /prelogin api call for the user.
    /// </summary>
    public string CipherConfiguration { get; set; }
    /// <summary>
    /// This contains Credential specific information. Storing as a blob gives us flexibility for future
    /// iterations of the specifics of the OPAQUE implementation.
    /// </summary>
    public string CredentialBlob { get; set; }

    /// <summary>
    /// User key encapsulated OPAQUE credential public key (enables user key rotation).
    /// </summary>
    public string EncryptedPublicKey { get; set; }

    /// <summary>
    ///  The OPAQUE clientside export key encapsulated OPAQUE credential private key.
    /// The client uses the export key to decrypt the private key and then decrypt the user key.
    /// </summary>
    public string EncryptedPrivateKey { get; set; }

    /// <summary>
    /// The OPAQUE Credential Public key encapsulated user key.
    /// The client uses the private key to decrypt the user key.
    /// </summary>
    public string EncryptedUserKey { get; set; }
    /// <summary>
    /// Date credential was created. When we update we are creating a new key set so in effect we are creating a new credential.
    /// </summary>
    public DateTime CreationDate { get; internal set; } = DateTime.UtcNow;

    public void SetNewId()
    {
        Id = CoreHelpers.GenerateComb();
    }
}
