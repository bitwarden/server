#nullable enable

namespace Bit.Infrastructure.EntityFramework.Vault.Models;

public class CipherArchive
{
    public Guid CipherId { get; set; }
    public Guid UserId { get; set; }
    public DateTime ArchivedDate { get; set; }
    public Cipher? Cipher { get; set; }
    public User? User { get; set; }
}
