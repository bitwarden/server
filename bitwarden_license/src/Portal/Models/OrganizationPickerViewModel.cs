using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Bit.Portal.Models
{
    public class OrganizationPickerViewModel
    {
        public string SelectedOrganization { get; set; }
        public List<SelectListItem> Organizations { get; set; }
    }
}
