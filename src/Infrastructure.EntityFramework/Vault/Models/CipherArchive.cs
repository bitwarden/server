#nullable disable

using Bit.Infrastructure.EntityFramework.Models;

namespace Bit.Infrastructure.EntityFramework.Vault.Models;

public class CipherArchive
{
    public Guid CipherId { get; set; }
    public Guid UserId { get; set; }
    public DateTime ArchivedDate { get; set; }

    // Navigation props
    public Cipher Cipher { get; set; }
    public User User { get; set; }   // optional, matches FK
}
