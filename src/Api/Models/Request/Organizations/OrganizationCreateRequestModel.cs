using Bit.Core.Domains;
using Bit.Core.Enums;
using System;

namespace Bit.Api.Models
{
    public class OrganizationCreateRequestModel
    {
        public string Name { get; set; }
        public PlanType Plan { get; set; }
        // TODO: Billing info for paid plans.

        public virtual Organization ToOrganization(Guid userId)
        {
            var organization = new Organization
            {
                UserId = userId,
                Name = Name,
                Plan = Plan
            };

            return organization;
        }
    }
}
