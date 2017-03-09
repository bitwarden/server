using System;
using Bit.Core.Models.Table;

namespace Bit.Api.Models
{
    public class OrganizationResponseModel : ResponseModel
    {
        public OrganizationResponseModel(Organization organization, string obj = "organization")
            : base(obj)
        {
            if(organization == null)
            {
                throw new ArgumentNullException(nameof(organization));
            }

            Id = organization.Id.ToString();
            Name = organization.Name;
            Plan = organization.Plan;
            PlanType = organization.PlanType;
            PlanTrial = organization.PlanTrial;
            MaxUsers = organization.MaxUsers;
        }

        public string Id { get; set; }
        public string Name { get; set; }
        public string Plan { get; set; }
        public Core.Enums.PlanType PlanType { get; set; }
        public bool PlanTrial { get; set; }
        public short MaxUsers { get; set; }
    }

    public class OrganizationExtendedResponseModel : OrganizationResponseModel
    {
        public OrganizationExtendedResponseModel(Organization organization, OrganizationUser organizationUser)
            : base(organization, "organizationExtended")
        {
            if(organizationUser == null)
            {
                throw new ArgumentNullException(nameof(organizationUser));
            }

            Key = organizationUser.Key;
        }

        public string Key { get; set; }
    }
}
