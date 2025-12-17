using System.Security.Claims;
using Bit.Core.AdminConsole.Context;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.Identity;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Microsoft.AspNetCore.Http;

namespace Bit.Core.Context;

/// <summary>
/// Provides information about the current HTTP request and the currently authenticated user (if any).
/// This is often (but not exclusively) parsed from the JWT in the current request.
/// </summary>
/// <remarks>
/// This interface suffers from having too much responsibility; consider whether any new code can go in a more
/// specific class rather than adding it here.
/// </remarks>
public interface ICurrentContext
{
    HttpContext HttpContext { get; set; }
    Guid? UserId { get; set; }
    User User { get; set; }
    string DeviceIdentifier { get; set; }
    DeviceType? DeviceType { get; set; }
    string IpAddress { get; set; }
    string CountryName { get; set; }
    List<CurrentContextOrganization> Organizations { get; set; }
    Guid? InstallationId { get; set; }
    Guid? OrganizationId { get; set; }
    IdentityClientType IdentityClientType { get; set; }
    string ClientId { get; set; }
    Version ClientVersion { get; set; }
    bool ClientVersionIsPrerelease { get; set; }

    Task BuildAsync(HttpContext httpContext, GlobalSettings globalSettings);
    Task BuildAsync(ClaimsPrincipal user, GlobalSettings globalSettings);

    Task SetContextAsync(ClaimsPrincipal user);

    Task<bool> OrganizationUser(Guid orgId);
    Task<bool> OrganizationAdmin(Guid orgId);
    Task<bool> OrganizationOwner(Guid orgId);
    Task<bool> OrganizationCustom(Guid orgId);
    Task<bool> AccessEventLogs(Guid orgId);
    Task<bool> AccessImportExport(Guid orgId);
    Task<bool> AccessReports(Guid orgId);
    [Obsolete("Deprecated. Use an authorization handler checking the specific permissions required instead.")]
    Task<bool> EditAnyCollection(Guid orgId);
    [Obsolete("Deprecated. Use an authorization handler checking the specific permissions required instead.")]
    Task<bool> ViewAllCollections(Guid orgId);
    Task<bool> ManageGroups(Guid orgId);
    Task<bool> ManagePolicies(Guid orgId);
    Task<bool> ManageSso(Guid orgId);
    Task<bool> ManageUsers(Guid orgId);
    Task<bool> AccessMembersTab(Guid orgId);
    Task<bool> ManageScim(Guid orgId);
    Task<bool> ManageResetPassword(Guid orgId);
    Task<bool> ViewSubscription(Guid orgId);
    Task<bool> EditSubscription(Guid orgId);
    Task<bool> EditPaymentMethods(Guid orgId);
    Task<bool> ViewBillingHistory(Guid orgId);
    /// <summary>
    /// Returns true if the current user is a member of a provider that manages the specified organization.
    /// This generally gives the user administrative privileges for the organization.
    /// </summary>
    /// <param name="orgId"></param>
    /// <returns></returns>
    Task<bool> ProviderUserForOrgAsync(Guid orgId);
    /// <summary>
    /// Returns true if the current user is a Provider Admin of the specified provider.
    /// </summary>
    bool ProviderProviderAdmin(Guid providerId);
    /// <summary>
    /// Returns true if the current user is a member of the specified provider (with any role).
    /// </summary>
    bool ProviderUser(Guid providerId);
    bool ProviderManageUsers(Guid providerId);
    bool ProviderAccessEventLogs(Guid providerId);
    bool AccessProviderOrganizations(Guid providerId);
    bool ManageProviderOrganizations(Guid providerId);

    Task<ICollection<CurrentContextOrganization>> OrganizationMembershipAsync(
        IOrganizationUserRepository organizationUserRepository, Guid userId);

    Task<ICollection<CurrentContextProvider>> ProviderMembershipAsync(
        IProviderUserRepository providerUserRepository, Guid userId);

    Task<Guid?> ProviderIdForOrg(Guid orgId);
    bool AccessSecretsManager(Guid organizationId);
    CurrentContextOrganization? GetOrganization(Guid orgId);
}
