using Bit.Core.Models.Table;
using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Models.Api
{
    public class OrganizationUpdateRequestModel
    {
        [Required]
        [StringLength(50)]
        public string Name { get; set; }
        [StringLength(50)]
        public string BusinessName { get; set; }
        [EmailAddress]
        [Required]
        [StringLength(50)]
        public string BillingEmail { get; set; }

        public virtual Organization ToOrganization(Organization existingOrganization)
        {
            existingOrganization.Name = Name;
            existingOrganization.BusinessName = BusinessName;
            existingOrganization.BillingEmail = BillingEmail?.ToLowerInvariant()?.Trim();
            return existingOrganization;
        }
    }
}
