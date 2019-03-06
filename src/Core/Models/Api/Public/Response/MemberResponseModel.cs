using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Table;

namespace Bit.Core.Models.Api.Public
{
    /// <summary>
    /// An organization member.
    /// </summary>
    public class MemberResponseModel : MemberBaseModel, IResponseModel
    {
        public MemberResponseModel(OrganizationUser user, IEnumerable<SelectionReadOnly> collections)
            : base(user)
        {
            if(user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            Id = user.Id;
            Email = user.Email;
            Status = user.Status;
            Collections = collections?.Select(c => new AssociationWithPermissionsResponseModel(c));
        }

        public MemberResponseModel(OrganizationUserUserDetails user, bool twoFactorEnabled,
            IEnumerable<SelectionReadOnly> collections)
            : base(user)
        {
            if(user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            Id = user.Id;
            Name = user.Name;
            Email = user.Email;
            TwoFactorEnabled = twoFactorEnabled;
            Status = user.Status;
            Collections = collections?.Select(c => new AssociationWithPermissionsResponseModel(c));
        }

        /// <summary>
        /// String representing the object's type. Objects of the same type share the same properties.
        /// </summary>
        /// <example>member</example>
        [Required]
        public string Object => "member";
        /// <summary>
        /// The member's unique identifier.
        /// </summary>
        /// <example>539a36c5-e0d2-4cf9-979e-51ecf5cf6593</example>
        [Required]
        public Guid Id { get; set; }
        /// <summary>
        /// The member's name, set from their user account profile.
        /// </summary>
        /// <example>John Smith</example>
        public string Name { get; set; }
        /// <summary>
        /// The member's email address.
        /// </summary>
        /// <example>jsmith@company.com</example>
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
    }
}
