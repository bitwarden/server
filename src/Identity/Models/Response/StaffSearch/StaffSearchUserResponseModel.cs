using Bit.Core.Entities;
using Bit.HttpExtensions;

namespace Bit.Identity.Models.Response.StaffSearch;

/// <summary>
/// An account the internal staff search found: what the Admin Portal's users index shows of it, and
/// nothing else (no keys, hashes, gateway identifiers or licence).
/// </summary>
public class StaffSearchUserResponseModel : ResponseModel
{
    public StaffSearchUserResponseModel(User user, bool twoFactorEnabled)
        : base("staffSearchUser")
    {
        ArgumentNullException.ThrowIfNull(user);

        Id = user.Id;
        Email = user.Email;
        CreationDate = user.CreationDate;
        Premium = user.Premium;
        PremiumExpirationDate = user.PremiumExpirationDate;
        MaxStorageGb = user.MaxStorageGb;
        EmailVerified = user.EmailVerified;
        TwoFactorEnabled = twoFactorEnabled;
    }

    public Guid Id { get; set; }
    public string Email { get; set; }
    public DateTime CreationDate { get; set; }
    public bool Premium { get; set; }
    public DateTime? PremiumExpirationDate { get; set; }
    public short? MaxStorageGb { get; set; }
    public bool EmailVerified { get; set; }

    /// <summary>Whether the account has two-step login enabled, as the index's lookup answers it.</summary>
    public bool TwoFactorEnabled { get; set; }
}
