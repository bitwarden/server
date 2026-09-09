using System.ComponentModel.DataAnnotations;
using Bit.Core.Utilities;

namespace Bit.Api.AdminConsole.Models.Request.Organizations;

public class ConfirmOrganizationInviteLinkRequestModel
{
    [Required]
    public required Guid OrganizationId { get; set; }

    [Required]
    public required Guid Code { get; set; }

    /// <summary>
    /// The organization symmetric key encrypted to the user.
    /// </summary>
    [Required]
    public required string OrgUserKey { get; set; }

    /// <summary>
    /// The user's account recovery key, supplied when the organization enforces automatic enrollment.
    /// </summary>
    public string? ResetPasswordKey { get; set; }

    /// <summary>
    /// The encrypted name of the default collection created when Organization Data Ownership applies.
    /// </summary>
    [Required]
    [EncryptedString]
    [EncryptedStringLength(1000)]
    public required string DefaultUserCollectionName { get; set; }
}
