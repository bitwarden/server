using System.Collections.Generic;
using Bit.Core.Models.Table;

namespace Bit.Core.Models.Api.Public
{
    public class MemberUpdateRequestModel : MemberBaseModel
    {
        /// <summary>
        /// The associated collections that this member can access.
        /// </summary>
        public IEnumerable<AssociationWithPermissionsRequestModel> Collections { get; set; }

        public virtual OrganizationUser ToOrganizationUser(OrganizationUser existingUser)
        {
            existingUser.Type = Type.Value;
            existingUser.AccessAll = AccessAll.Value;
            existingUser.ExternalId = ExternalId;
            return existingUser;
        }
    }
}
