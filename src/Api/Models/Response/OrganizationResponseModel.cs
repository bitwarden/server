using System;
using Bit.Core.Domains;
using Bit.Core.Enums;

namespace Bit.Api.Models
{
    public class OrganizationResponseModel : ResponseModel
    {
        public OrganizationResponseModel(Organization organization)
            : base("organization")
        {
            if(organization == null)
            {
                throw new ArgumentNullException(nameof(organization));
            }

            Id = organization.Id.ToString();
            Name = organization.Name;
            Plan = organization.Plan;
            MaxUsers = organization.MaxUsers;
        }

        public string Id { get; set; }
        public string Name { get; set; }
        public PlanType Plan { get; set; }
        public short MaxUsers { get; set; }
    }
}
