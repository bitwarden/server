#nullable disable

using Bit.Infrastructure.EntityFramework.Vault.Models;

public class CipherArchive
{
    public Guid CipherId { get; set; }
    public Guid UserId { get; set; }
    public DateTime ArchivedDate { get; set; }

    // Optional navigation props – you can drop these if you don't want them.
    public Cipher Cipher { get; set; }
}
