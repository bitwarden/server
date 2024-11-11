using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Bit.Api.Models.Public.Response;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;

namespace Bit.Api.AdminConsole.Public.Models.Response;

/// <summary>
/// An organization member.
/// </summary>
public class MemberResponseModel : MemberBaseModel, IResponseModel
{
    [JsonConstructor]
    public MemberResponseModel() { }

    public MemberResponseModel(OrganizationUser user, IEnumerable<CollectionAccessSelection> collections) : base(user)
    {
        if (user == null)
        {
            throw new ArgumentNullException(nameof(user));
        }

        Id = user.Id;
        UserId = user.UserId;
        Email = user.Email;
        Status = user.Status;
        Collections = collections?.Select(c => new AssociationWithPermissionsResponseModel(c));
        ResetPasswordEnrolled = user.ResetPasswordKey != null;
    }

    public MemberResponseModel(OrganizationUserUserDetails user, bool twoFactorEnabled,
        IEnumerable<CollectionAccessSelection> collections) : base(user)
    {
        if (user == null)
        {
            throw new ArgumentNullException(nameof(user));
        }

        Id = user.Id;
        UserId = user.UserId;
        Name = user.Name;
        Email = user.Email;
        TwoFactorEnabled = twoFactorEnabled;
        Status = user.Status;
        Collections = collections?.Select(c => new AssociationWithPermissionsResponseModel(c));
        ResetPasswordEnrolled = user.ResetPasswordKey != null;
    }

    /// <summary>
    /// String representing the object's type. Objects of the same type share the same properties.
    /// </summary>
    /// <example>member</example>
    [Required]
    public string Object => "member";
    /// <summary>
    /// The member's unique identifier within the organization.
    /// </summary>
    /// <example>539a36c5-e0d2-4cf9-979e-51ecf5cf6593</example>
    [Required]
    public Guid Id { get; set; }
    /// <summary>
    /// The member's unique identifier across Bitwarden.
    /// </summary>
    /// <example>48b47ee1-493e-4c67-aef7-014996c40eca</example>
    [Required]
    public Guid? UserId { get; set; }
    /// <summary>
    /// The member's name, set from their user account profile.
    /// </summary>
    /// <example>John Smith</example>
    public string Name { get; set; }
    /// <summary>
    /// The member's email address.
    /// </summary>
    /// <example>jsmith@example.com</example>
    [Required]
    public string Email { get; set; }
    /// <summary>
    /// Returns <c>true</c> if the member has a two-step login method enabled on their user account.
    /// </summary>
    [Required]
    public bool TwoFactorEnabled { get; set; }
    /// <summary>
    /// The member's status within the organization. All created members start with a status of "Invited".
    /// Once a member accept's their invitation to join the organization, their status changes to "Accepted".
    /// Accepted members are then "Confirmed" by an organization administrator. Once a member is "Confirmed",
    /// their status can no longer change.
    /// </summary>
    [Required]
    public OrganizationUserStatusType Status { get; set; }
    /// <summary>
    /// The associated collections that this member can access.
    /// </summary>
    public IEnumerable<AssociationWithPermissionsResponseModel> Collections { get; set; }

    /// <summary>
    /// Returns <c>true</c> if the member has enrolled in Password Reset assistance within the organization
    /// </summary>
    [Required]
    public bool ResetPasswordEnrolled { get; }
}
