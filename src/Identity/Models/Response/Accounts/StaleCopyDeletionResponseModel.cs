using Bit.Core.Models.Api;

namespace Bit.Identity.Models.Response.Accounts;

/// <summary>What the internal stale copy deletion did: counts, and nothing of the rows.</summary>
public class StaleCopyDeletionResponseModel : ResponseModel
{
    public StaleCopyDeletionResponseModel(
        int providersDeleted,
        int organizationsDeleted,
        int usersDeleted,
        int alreadyGone,
        int ciphersWithAttachmentsDeleted)
        : base("staleCopyDeletion")
    {
        ProvidersDeleted = providersDeleted;
        OrganizationsDeleted = organizationsDeleted;
        UsersDeleted = usersDeleted;
        AlreadyGone = alreadyGone;
        CiphersWithAttachmentsDeleted = ciphersWithAttachmentsDeleted;
    }

    /// <summary>The providers deleted.</summary>
    public int ProvidersDeleted { get; set; }

    /// <summary>The organizations deleted, with everything they held.</summary>
    public int OrganizationsDeleted { get; set; }

    /// <summary>The users deleted, with everything they held.</summary>
    public int UsersDeleted { get; set; }

    /// <summary>The distinct identifiers named that this install no longer holds: nothing to delete.</summary>
    public int AlreadyGone { get; set; }

    /// <summary>The ciphers, the organizations' and the users' own, whose attachment files were deleted.</summary>
    public int CiphersWithAttachmentsDeleted { get; set; }
}
