#nullable enable

using System.ComponentModel.DataAnnotations;

namespace Bit.Identity.Models.Request.Accounts;

/// <summary>An address to look up, and the organization whose claim on its domain doesn't count.</summary>
public class AccountLookupRequestModel
{
    /// <summary>The address, as an account would sign in with it.</summary>
    [Required]
    [StringLength(256)]
    public string Email { get; set; } = null!;

    /// <summary>
    /// The organization inviting the address, whose claim on the domain doesn't block it: this server's
    /// <c>excludeOrganizationId</c>.
    /// </summary>
    public Guid? ExcludeOrganizationId { get; set; }

    /// <summary>Names nothing: the address is somebody's.</summary>
    public override string ToString() => nameof(AccountLookupRequestModel);
}
