using Bit.Core.Models.Interfaces;
using Bit.Core.Models.Table;
using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Models.Api
{
    public class OrganizationUpdateRequestModel: IPermissions
    {
        [Required]
        [StringLength(50)]
        public string Name { get; set; }
        [StringLength(50)]
        public string BusinessName { get; set; }
        [StringLength(50)]
        public string Identifier { get; set; }
        [EmailAddress]
        [Required]
        [StringLength(50)]
        public string BillingEmail { get; set; }

        public bool AccessBusinessPortal { get; set; }
        public bool AccessEventLogs { get; set; }
        public bool AccessImportExport { get; set; }
        public bool AccessReports { get; set; }
        public bool ManageAllCollections { get; set; }
        public bool ManageAssignedCollections { get; set; }
        public bool ManageGroups { get; set; }
        public bool ManagePolicies { get; set; }
        public bool ManageUsers { get; set; }
        public bool ViewPolicies { get; set; }

        public virtual Organization ToOrganization(Organization existingOrganization, GlobalSettings globalSettings)
        {
            if (!globalSettings.SelfHosted)
            {
                // These items come from the license file
                existingOrganization.Name = Name;
                existingOrganization.BusinessName = BusinessName;
                existingOrganization.BillingEmail = BillingEmail?.ToLowerInvariant()?.Trim();
            }
            existingOrganization.Identifier = Identifier;
            return existingOrganization;
        }
    }
}
