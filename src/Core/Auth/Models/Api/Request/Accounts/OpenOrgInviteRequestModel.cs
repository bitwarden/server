namespace Bit.Core.Auth.Models.Api.Request.Accounts;

/// <summary>
/// Identifying key for an open organization invite link: the target organization and the
/// link's bearer code. Sent as the register-finish payload directly, and extended by
/// <see cref="RegisterStartOpenOrgInviteRequestModel"/> with a sealed data blob at register-start.
/// </summary>
public class OpenOrgInviteRequestModel
{
    public required Guid OrganizationId { get; set; }

    public required Guid Code { get; set; }
}
