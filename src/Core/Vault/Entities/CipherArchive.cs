#nullable enable

using Bit.Core.Entities;

namespace Bit.Core.Vault.Entities;

public class CipherArchive
{
    public Guid CipherId { get; set; }
    public Guid UserId { get; set; }
    public DateTime ArchivedDate { get; set; }

    public Cipher? Cipher { get; set; }
    public User? User { get; set; }
}
