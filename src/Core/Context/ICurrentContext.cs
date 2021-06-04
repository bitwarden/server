using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Bit.Core.Enums;
using Bit.Core.Models.Table;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Microsoft.AspNetCore.Http;

namespace Bit.Core.Context
{
    public interface ICurrentContext
    {
        HttpContext HttpContext { get; set; }
        Guid? UserId { get; set; }
        User User { get; set; }
        string DeviceIdentifier { get; set; }
        DeviceType? DeviceType { get; set; }
        string IpAddress { get; set; }
        List<CurrentContentOrganization> Organizations { get; set; }
        Guid? InstallationId { get; set; }
        Guid? OrganizationId { get; set; }

        Task BuildAsync(HttpContext httpContext, GlobalSettings globalSettings);
        Task BuildAsync(ClaimsPrincipal user, GlobalSettings globalSettings);

        Task SetContextAsync(ClaimsPrincipal user);


        bool OrganizationUser(Guid orgId);
        bool OrganizationManager(Guid orgId);
        bool OrganizationAdmin(Guid orgId);
        bool OrganizationOwner(Guid orgId);
        bool OrganizationCustom(Guid orgId);
        bool AccessBusinessPortal(Guid orgId);
        bool AccessEventLogs(Guid orgId);
        bool AccessImportExport(Guid orgId);
        bool AccessReports(Guid orgId);
        bool ManageAllCollections(Guid orgId);
        bool ManageAssignedCollections(Guid orgId);
        bool ManageGroups(Guid orgId);
        bool ManagePolicies(Guid orgId);
        bool ManageSso(Guid orgId);
        bool ManageUsers(Guid orgId);
        bool ManageResetPassword(Guid orgId);
        bool ProviderProviderAdmin(Guid providerId);
        bool ProviderUser(Guid providerId);

        Task<ICollection<CurrentContentOrganization>> OrganizationMembershipAsync(
            IOrganizationUserRepository organizationUserRepository, Guid userId);

        Task<ICollection<CurrentContentProvider>> ProviderMembershipAsync(
            IProviderUserRepository providerUserRepository, Guid userId);
    }
}
