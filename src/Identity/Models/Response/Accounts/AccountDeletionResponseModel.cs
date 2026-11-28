using Bit.Core.Models.Api;

namespace Bit.Identity.Models.Response.Accounts;

/// <summary>What the internal account deletion did: one fact, and nothing of the account.</summary>
public class AccountDeletionResponseModel : ResponseModel
{
    public AccountDeletionResponseModel(bool deleted)
        : base("accountDeletion")
    {
        Deleted = deleted;
    }

    /// <summary>
    /// Whether the account was deleted now: false when there was no account with the identifier.
    /// </summary>
    public bool Deleted { get; set; }
}
