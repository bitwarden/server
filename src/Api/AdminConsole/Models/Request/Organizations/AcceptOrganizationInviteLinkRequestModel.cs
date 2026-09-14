using System.ComponentModel.DataAnnotations;
using Bit.Core.Utilities;

namespace Bit.Api.AdminConsole.Models.Request.Organizations;

public class AcceptOrganizationInviteLinkRequestModel
{
    [Required]
    public required Guid OrganizationId { get; set; }

    [Required]
    public required Guid Code { get; set; }

    [EncryptedStringLength(1000)]
    public string? ResetPasswordKey { get; set; }
}
