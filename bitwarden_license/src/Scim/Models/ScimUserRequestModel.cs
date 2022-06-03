using System;
using Bit.Core.Entities;

namespace Bit.Scim.Models
{
    public class ScimUserRequestModel : BaseScimUserModel
    {
        public ScimUserRequestModel()
            : base(false)
        { }

        public OrganizationUser ToOrganizationUser()
        {
            return new OrganizationUser
            {
                ExternalId = UserName,
                Email = PrimaryEmail,
                Type = Core.Enums.OrganizationUserType.User,
            };
        }
    }
}
