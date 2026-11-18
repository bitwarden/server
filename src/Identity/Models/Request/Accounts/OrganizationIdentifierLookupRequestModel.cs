#nullable enable

using System.ComponentModel.DataAnnotations;

namespace Bit.Identity.Models.Request.Accounts;

/// <summary>An organization's single sign-on identifier to look up.</summary>
public class OrganizationIdentifierLookupRequestModel
{
    /// <summary>The identifier, as an organization's administrator sets it.</summary>
    [Required]
    [StringLength(50)]
    public string Identifier { get; set; } = null!;
}
