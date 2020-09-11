using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bit.Portal.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Bit.Portal.Components
{
    public class OrganizationPickerViewComponent : ViewComponent
    {
        private readonly EnterprisePortalCurrentContext _enterprisePortalCurrentContext;

        public OrganizationPickerViewComponent(EnterprisePortalCurrentContext enterprisePortalCurrentContext)
        {
            _enterprisePortalCurrentContext = enterprisePortalCurrentContext;
        }

        public Task<IViewComponentResult> InvokeAsync()
        {
            return Task.FromResult(View(new OrganizationPickerViewModel
            {
                SelectedOrganization = _enterprisePortalCurrentContext?.SelectedOrganizationId?.ToString(),
                Organizations = _enterprisePortalCurrentContext?.OrganizationsDetails?.Where(x => x.UseBusinessPortal)
                    .Select(o => new SelectListItem
                    {
                        Value = o.OrganizationId.ToString(),
                        Text = o.Name
                    }).ToList() ?? new List<SelectListItem>()
            }) as IViewComponentResult);
        }
    }
}
