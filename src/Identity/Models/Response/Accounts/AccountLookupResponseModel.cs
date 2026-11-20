using Bit.Core.Models.Api;

namespace Bit.Identity.Models.Response.Accounts;

/// <summary>What the lookup found: two facts, which account signs in with the address, and when it was made.</summary>
public class AccountLookupResponseModel : ResponseModel
{
    public AccountLookupResponseModel(bool accountExists, bool accountCreationBlocked, DateTime? accountCreationDate = null,
        Guid? accountId = null)
        : base("accountLookup")
    {
        AccountExists = accountExists;
        AccountCreationBlocked = accountCreationBlocked;
        AccountCreationDate = accountCreationDate;
        AccountId = accountId;
    }

    /// <summary>Whether an account signs in with the address.</summary>
    public bool AccountExists { get; set; }

    /// <summary>
    /// The account that signs in with the address, as this server's database compares addresses, or null.
    /// </summary>
    public Guid? AccountId { get; set; }

    /// <summary>
    /// Whether an organization, other than the one excluded, has verified the address's domain and
    /// blocks account creation on it: the registration's "This email address is claimed by an
    /// organization using Bitwarden.".
    /// </summary>
    public bool AccountCreationBlocked { get; set; }

    /// <summary>
    /// When the account with the address was made (<c>User.CreationDate</c>, UTC), or <c>null</c> when there is none.
    /// For the caller to compare with dates of its own.
    /// </summary>
    public DateTime? AccountCreationDate { get; set; }
}
