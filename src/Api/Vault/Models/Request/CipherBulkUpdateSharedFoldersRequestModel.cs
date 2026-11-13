// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

namespace Bit.Api.Vault.Models.Request;

public class CipherBulkUpdateSharedFoldersRequestModel
{
    public Guid OrganizationId { get; set; }

    public IEnumerable<Guid> CipherIds { get; set; }

    public IEnumerable<Guid> SharedFolderIds { get; set; }

    /// <summary>
    /// If true, the shared folders will be removed from the ciphers. Otherwise, they will be added.
    /// </summary>
    public bool RemoveSharedFolders { get; set; }
}
