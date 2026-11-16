namespace Bit.Core.Vault.Models;

/// <summary>
/// The per-user fields of a cipher after <see cref="UserId"/> changed them. These values are plaintext
/// metadata, not vault data, so a client can apply them to its stored cipher without a sync.
/// </summary>
public class SyncCipherUserFieldsPushNotification
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? FolderId { get; set; }
    public bool Favorite { get; set; }
    public DateTime? ArchivedDate { get; set; }
}
