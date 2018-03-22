using Bit.Core.Models.Table;

namespace Bit.Admin.Models
{
    public class OrganizationsModel : PagedModel<Organization>
    {
        public string Name { get; set; }
        public string UserEmail { get; set; }
        public bool? Paid { get; set; }
    }
}
