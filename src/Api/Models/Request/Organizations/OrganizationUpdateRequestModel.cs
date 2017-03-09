using Bit.Core.Models.Table;

namespace Bit.Api.Models
{
    public class OrganizationUpdateRequestModel
    {
        public string Name { get; set; }

        public virtual Organization ToOrganization(Organization existingOrganization)
        {
            existingOrganization.Name = Name;
            return existingOrganization;
        }
    }
}
