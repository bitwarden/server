using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;

namespace Bit.Core.Auth.Models.Data;

/// <summary>
/// Represents an <see cref="AuthRequestType.AdminApproval"/> AuthRequest.
/// Includes additional user and organization information.
/// </summary>
public class OrganizationAdminAuthRequest : AuthRequest
{
    /// <summary>
    /// Email address of the requesting user
    /// </summary>
    public string Email { get; set; }

    public Guid OrganizationUserId { get; set; }
}
