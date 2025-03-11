using Bit.Core.Entities;
using Bit.Core.Utilities;

namespace Bit.Core.Auth.Entities;

public class OpaqueKeyExchangeCredential : ITableObject<Guid>
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string CipherConfiguration { get; set; }
    public string CredentialBlob { get; set; }
    public string PublicKey { get; set; }
    public string PrivateKey { get; set; }
    public string EncryptedUserKey { get; set; }
    public DateTime CreationDate { get; internal set; } = DateTime.UtcNow;
    public DateTime RevisionDate { get; internal set; } = DateTime.UtcNow;

    public void SetNewId()
    {
        Id = CoreHelpers.GenerateComb();
    }
}
