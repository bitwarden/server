using Bit.Core.Models.Table;
using Bit.Core.Enums;
using Bit.Core.Models.Business;
using System;

namespace Bit.Core.Models.Api
{
    public class OrganizationCreateRequestModel
    {
        public string Name { get; set; }
        public PlanType PlanType { get; set; }
        public string Key { get; set; }

        public virtual OrganizationSignup ToOrganizationSignup(User user)
        {
            return new OrganizationSignup
            {
                Owner = user,
                OwnerKey = Key,
                Name = Name,
                Plan = PlanType
            };
        }
    }
}
