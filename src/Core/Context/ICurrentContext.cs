using System.Security.Claims;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Microsoft.AspNetCore.Http;

namespace Bit.Core.Context;

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
    bool IsBot { get; set; }
    bool MaybeBot { get; set; }
    int? BotScore { get; set; }
    string ClientId { get; set; }
    Version ClientVersion { get; set; }
    Task BuildAsync(HttpContext httpContext, GlobalSettings globalSettings);
    Task BuildAsync(ClaimsPrincipal user, GlobalSettings globalSettings);

    Task SetContextAsync(ClaimsPrincipal user);


    Task<bool> OrganizationUser(Guid orgId);
    Task<bool> OrganizationManager(Guid orgId);
    Task<bool> OrganizationAdmin(Guid orgId);
    Task<bool> OrganizationOwner(Guid orgId);
    Task<bool> OrganizationCustom(Guid orgId);
    Task<bool> AccessEventLogs(Guid orgId);
    Task<bool> AccessImportExport(Guid orgId);
    Task<bool> AccessReports(Guid orgId);
    Task<bool> CreateNewCollections(Guid orgId);
    Task<bool> EditAnyCollection(Guid orgId);
    Task<bool> DeleteAnyCollection(Guid orgId);
    Task<bool> ViewAllCollections(Guid orgId);
    Task<bool> EditAssignedCollections(Guid orgId);
    Task<bool> DeleteAssignedCollections(Guid orgId);
    Task<bool> ViewAssignedCollections(Guid orgId);
    Task<bool> ManageGroups(Guid orgId);
    Task<bool> ManagePolicies(Guid orgId);
    Task<bool> ManageSso(Guid orgId);
    Task<bool> ManageUsers(Guid orgId);
    Task<bool> ManageScim(Guid orgId);
    Task<bool> ManageResetPassword(Guid orgId);
    Task<bool> ManageBilling(Guid orgId);
    Task<bool> ProviderUserForOrgAsync(Guid orgId);
    bool ProviderProviderAdmin(Guid providerId);
    bool ProviderUser(Guid providerId);
    bool ProviderManageUsers(Guid providerId);
    bool ProviderAccessEventLogs(Guid providerId);
    bool AccessProviderOrganizations(Guid providerId);
    bool ManageProviderOrganizations(Guid providerId);

    Task<ICollection<CurrentContentOrganization>> OrganizationMembershipAsync(
        IOrganizationUserRepository organizationUserRepository, Guid userId);

    Task<ICollection<CurrentContentProvider>> ProviderMembershipAsync(
        IProviderUserRepository providerUserRepository, Guid userId);

    Task<Guid?> ProviderIdForOrg(Guid orgId);
}
