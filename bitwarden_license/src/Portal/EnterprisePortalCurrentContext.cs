using System;
using Bit.Core.Context;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Bit.Core.Repositories;
using System.Linq;
using System.Collections.Generic;
using Bit.Core.Models.Data;
using Bit.Core.Utilities;

namespace Bit.Portal
{
    public class EnterprisePortalCurrentContext : CurrentContext
    {
        private readonly IServiceProvider _serviceProvider;

        public EnterprisePortalCurrentContext(IProviderOrganizationRepository providerOrganizationRepository,
            IServiceProvider serviceProvider) : base(providerOrganizationRepository)
        {
            _serviceProvider = serviceProvider;
        }

        public Guid? SelectedOrganizationId { get; set; }
        public OrganizationUserOrganizationDetails SelectedOrganizationDetails { get; set; }

        public List<OrganizationUserOrganizationDetails> OrganizationsDetails { get; set; }

        public bool ManagerForSelectedOrganization =>
            SelectedOrganizationDetails?.Type == Core.Enums.OrganizationUserType.Manager ||
            SelectedOrganizationDetails?.Type == Core.Enums.OrganizationUserType.Admin ||
            SelectedOrganizationDetails?.Type == Core.Enums.OrganizationUserType.Owner;

        public bool AdminForSelectedOrganization =>
            SelectedOrganizationDetails?.Type == Core.Enums.OrganizationUserType.Admin ||
            SelectedOrganizationDetails?.Type == Core.Enums.OrganizationUserType.Owner;

        public bool OwnerForSelectedOrganization =>
            SelectedOrganizationDetails?.Type == Core.Enums.OrganizationUserType.Owner;

        public bool CanManagePoliciesForSelectedOrganization =>
            AdminForSelectedOrganization || SelectedOrganizationDetailsPermissions.ManagePolicies == true;

        public bool CanManageSsoForSelectedOrganization =>
            AdminForSelectedOrganization || SelectedOrganizationDetailsPermissions.ManageSso == true;

        public Permissions SelectedOrganizationDetailsPermissions => CoreHelpers.LoadClassFromJsonData<Permissions>(SelectedOrganizationDetails?.Permissions);

        public async override Task SetContextAsync(ClaimsPrincipal user)
        {
            var nameId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (Guid.TryParse(nameId, out var nameIdGuid))
            {
                UserId = nameIdGuid;
            }

            if (!UserId.HasValue)
            {
                return;
            }

            // TODO: maybe make loading orgs Lazy<T> somehow?
            var orgUserRepo = _serviceProvider.GetRequiredService<IOrganizationUserRepository>();
            var userOrgs = await orgUserRepo.GetManyDetailsByUserAsync(UserId.Value,
                Core.Enums.OrganizationUserStatusType.Confirmed);
            OrganizationsDetails = userOrgs.ToList();
            Organizations = userOrgs.Select(ou => new CurrentContentOrganization
            {
                Id = ou.OrganizationId,
                Type = ou.Type
            }).ToList();

            if (SelectedOrganizationId == null && HttpContext.Request.Cookies.ContainsKey("SelectedOrganization") &&
                Guid.TryParse(HttpContext.Request.Cookies["SelectedOrganization"], out var selectedOrgId))
            {
                SelectedOrganizationId = Organizations.FirstOrDefault(o => o.Id == selectedOrgId)?.Id;
                SelectedOrganizationDetails = OrganizationsDetails.FirstOrDefault(
                    o => o.OrganizationId == SelectedOrganizationId);
            }

            if (DeviceIdentifier == null && HttpContext.Request.Cookies.ContainsKey("DeviceIdentifier"))
            {
                DeviceIdentifier = HttpContext.Request.Cookies["DeviceIdentifier"];
            }

            DeviceType = Core.Enums.DeviceType.UnknownBrowser;
            if (HttpContext.Request.Headers.ContainsKey("User-Agent"))
            {
                var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();
                if (userAgent.Contains(" Firefox/") || userAgent.Contains(" Gecko/"))
                {
                    DeviceType = Core.Enums.DeviceType.FirefoxBrowser;
                }
                else if (userAgent.IndexOf(" OPR/") >= 0)
                {
                    DeviceType = Core.Enums.DeviceType.OperaBrowser;
                }
                else if (userAgent.Contains(" Edge/"))
                {
                    DeviceType = Core.Enums.DeviceType.EdgeBrowser;
                }
                else if (userAgent.Contains(" Vivaldi/"))
                {
                    DeviceType = Core.Enums.DeviceType.VivaldiBrowser;
                }
                else if (userAgent.Contains(" Safari/") && !userAgent.Contains("Chrome"))
                {
                    DeviceType = Core.Enums.DeviceType.SafariBrowser;
                }
                else if (userAgent.Contains(" Chrome/"))
                {
                    DeviceType = Core.Enums.DeviceType.ChromeBrowser;
                }
                else if (userAgent.Contains(" Trident/"))
                {
                    DeviceType = Core.Enums.DeviceType.IEBrowser;
                }
            }
        }
    }
}
