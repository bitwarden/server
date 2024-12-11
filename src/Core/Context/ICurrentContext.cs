#nullable enable

using System.Security.Claims;
using Bit.Core.AdminConsole.Context;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Identity;
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
    List<CurrentContextOrganization> Organizations { get; set; }
    Guid? InstallationId { get; set; }
    Guid? OrganizationId { get; set; }
    IdentityClientType IdentityClientType { get; set; }
    bool IsBot { get; set; }
    bool MaybeBot { get; set; }
    int? BotScore { get; set; }
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
    Task<bool> EditAnyCollection(Guid orgId);
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
    Task<bool> ProviderUserForOrgAsync(Guid orgId);
    bool ProviderProviderAdmin(Guid providerId);
    bool ProviderUser(Guid providerId);
    bool ProviderManageUsers(Guid providerId);
    bool ProviderAccessEventLogs(Guid providerId);
    bool AccessProviderOrganizations(Guid providerId);
    bool ManageProviderOrganizations(Guid providerId);

    Task<ICollection<CurrentContextOrganization>> OrganizationMembershipAsync(
        IOrganizationUserRepository organizationUserRepository,
        Guid userId
    );

    Task<ICollection<CurrentContextProvider>> ProviderMembershipAsync(
        IProviderUserRepository providerUserRepository,
        Guid userId
    );

    Task<Guid?> ProviderIdForOrg(Guid orgId);
    bool AccessSecretsManager(Guid organizationId);
    CurrentContextOrganization? GetOrganization(Guid orgId);
}
