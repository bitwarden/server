using System.Security.Claims;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Enums.Provider;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Http;

namespace Bit.Core.Context;

public class CurrentContext : ICurrentContext
{
    private readonly IProviderUserRepository _providerUserRepository;
    private bool _builtHttpContext;
    private bool _builtClaimsPrincipal;
    private IEnumerable<ProviderUserOrganizationDetails> _providerUserOrganizations;

    public virtual HttpContext HttpContext { get; set; }
    public virtual Guid? UserId { get; set; }
    public virtual User User { get; set; }
    public virtual string DeviceIdentifier { get; set; }
    public virtual DeviceType? DeviceType { get; set; }
    public virtual string IpAddress { get; set; }
    public virtual List<CurrentContentOrganization> Organizations { get; set; }
    public virtual List<CurrentContentProvider> Providers { get; set; }
    public virtual Guid? InstallationId { get; set; }
    public virtual Guid? OrganizationId { get; set; }
    public virtual bool CloudflareWorkerProxied { get; set; }
    public virtual bool IsBot { get; set; }
    public virtual bool MaybeBot { get; set; }
    public virtual int? BotScore { get; set; }
    public virtual string ClientId { get; set; }
    public virtual Version ClientVersion { get; set; }

    public CurrentContext(IProviderUserRepository providerUserRepository)
    {
        _providerUserRepository = providerUserRepository;
    }

    public async virtual Task BuildAsync(HttpContext httpContext, GlobalSettings globalSettings)
    {
        if (_builtHttpContext)
        {
            return;
        }

        _builtHttpContext = true;
        HttpContext = httpContext;
        await BuildAsync(httpContext.User, globalSettings);

        if (DeviceIdentifier == null && httpContext.Request.Headers.ContainsKey("Device-Identifier"))
        {
            DeviceIdentifier = httpContext.Request.Headers["Device-Identifier"];
        }

        if (httpContext.Request.Headers.ContainsKey("Device-Type") &&
            Enum.TryParse(httpContext.Request.Headers["Device-Type"].ToString(), out DeviceType dType))
        {
            DeviceType = dType;
        }

        if (!BotScore.HasValue && httpContext.Request.Headers.ContainsKey("X-Cf-Bot-Score") &&
            int.TryParse(httpContext.Request.Headers["X-Cf-Bot-Score"], out var parsedBotScore))
        {
            BotScore = parsedBotScore;
        }

        if (httpContext.Request.Headers.ContainsKey("X-Cf-Worked-Proxied"))
        {
            CloudflareWorkerProxied = httpContext.Request.Headers["X-Cf-Worked-Proxied"] == "1";
        }

        if (httpContext.Request.Headers.ContainsKey("X-Cf-Is-Bot"))
        {
            IsBot = httpContext.Request.Headers["X-Cf-Is-Bot"] == "1";
        }

        if (httpContext.Request.Headers.ContainsKey("X-Cf-Maybe-Bot"))
        {
            MaybeBot = httpContext.Request.Headers["X-Cf-Maybe-Bot"] == "1";
        }

        if (httpContext.Request.Headers.ContainsKey("Bitwarden-Client-Version"))
        {
            ClientVersion = new Version(httpContext.Request.Headers["Bitwarden-Client-Version"]);
        }
    }

    public async virtual Task BuildAsync(ClaimsPrincipal user, GlobalSettings globalSettings)
    {
        if (_builtClaimsPrincipal)
        {
            return;
        }

        _builtClaimsPrincipal = true;
        IpAddress = HttpContext.GetIpAddress(globalSettings);
        await SetContextAsync(user);
    }

    public virtual Task SetContextAsync(ClaimsPrincipal user)
    {
        if (user == null || !user.Claims.Any())
        {
            return Task.FromResult(0);
        }

        var claimsDict = user.Claims.GroupBy(c => c.Type).ToDictionary(c => c.Key, c => c.Select(v => v));

        var subject = GetClaimValue(claimsDict, "sub");
        if (Guid.TryParse(subject, out var subIdGuid))
        {
            UserId = subIdGuid;
        }

        ClientId = GetClaimValue(claimsDict, "client_id");
        var clientSubject = GetClaimValue(claimsDict, "client_sub");
        var orgApi = false;
        if (clientSubject != null)
        {
            if (ClientId?.StartsWith("installation.") ?? false)
            {
                if (Guid.TryParse(clientSubject, out var idGuid))
                {
                    InstallationId = idGuid;
                }
            }
            else if (ClientId?.StartsWith("organization.") ?? false)
            {
                if (Guid.TryParse(clientSubject, out var idGuid))
                {
                    OrganizationId = idGuid;
                    orgApi = true;
                }
            }
        }

        DeviceIdentifier = GetClaimValue(claimsDict, "device");

        Organizations = GetOrganizations(claimsDict, orgApi);

        Providers = GetProviders(claimsDict);

        return Task.FromResult(0);
    }

    private List<CurrentContentOrganization> GetOrganizations(Dictionary<string, IEnumerable<Claim>> claimsDict, bool orgApi)
    {
        var organizations = new List<CurrentContentOrganization>();
        if (claimsDict.ContainsKey("orgowner"))
        {
            organizations.AddRange(claimsDict["orgowner"].Select(c =>
                new CurrentContentOrganization
                {
                    Id = new Guid(c.Value),
                    Type = OrganizationUserType.Owner
                }));
        }
        else if (orgApi && OrganizationId.HasValue)
        {
            organizations.Add(new CurrentContentOrganization
            {
                Id = OrganizationId.Value,
                Type = OrganizationUserType.Owner
            });
        }

        if (claimsDict.ContainsKey("orgadmin"))
        {
            organizations.AddRange(claimsDict["orgadmin"].Select(c =>
                new CurrentContentOrganization
                {
                    Id = new Guid(c.Value),
                    Type = OrganizationUserType.Admin
                }));
        }

        if (claimsDict.ContainsKey("orguser"))
        {
            organizations.AddRange(claimsDict["orguser"].Select(c =>
                new CurrentContentOrganization
                {
                    Id = new Guid(c.Value),
                    Type = OrganizationUserType.User
                }));
        }

        if (claimsDict.ContainsKey("orgmanager"))
        {
            organizations.AddRange(claimsDict["orgmanager"].Select(c =>
                new CurrentContentOrganization
                {
                    Id = new Guid(c.Value),
                    Type = OrganizationUserType.Manager
                }));
        }

        if (claimsDict.ContainsKey("orgcustom"))
        {
            organizations.AddRange(claimsDict["orgcustom"].Select(c =>
                new CurrentContentOrganization
                {
                    Id = new Guid(c.Value),
                    Type = OrganizationUserType.Custom,
                    Permissions = SetOrganizationPermissionsFromClaims(c.Value, claimsDict)
                }));
        }

        return organizations;
    }

    private List<CurrentContentProvider> GetProviders(Dictionary<string, IEnumerable<Claim>> claimsDict)
    {
        var providers = new List<CurrentContentProvider>();
        if (claimsDict.ContainsKey("providerprovideradmin"))
        {
            providers.AddRange(claimsDict["providerprovideradmin"].Select(c =>
                new CurrentContentProvider
                {
                    Id = new Guid(c.Value),
                    Type = ProviderUserType.ProviderAdmin
                }));
        }

        if (claimsDict.ContainsKey("providerserviceuser"))
        {
            providers.AddRange(claimsDict["providerserviceuser"].Select(c =>
                new CurrentContentProvider
                {
                    Id = new Guid(c.Value),
                    Type = ProviderUserType.ServiceUser
                }));
        }

        return providers;
    }

    public async Task<bool> OrganizationUser(Guid orgId)
    {
        return (Organizations?.Any(o => o.Id == orgId) ?? false) || await OrganizationOwner(orgId);
    }

    public async Task<bool> OrganizationManager(Guid orgId)
    {
        return await OrganizationAdmin(orgId) ||
               (Organizations?.Any(o => o.Id == orgId && o.Type == OrganizationUserType.Manager) ?? false);
    }

    public async Task<bool> OrganizationAdmin(Guid orgId)
    {
        return await OrganizationOwner(orgId) ||
               (Organizations?.Any(o => o.Id == orgId && o.Type == OrganizationUserType.Admin) ?? false);
    }

    public async Task<bool> OrganizationOwner(Guid orgId)
    {
        if (Organizations?.Any(o => o.Id == orgId && o.Type == OrganizationUserType.Owner) ?? false)
        {
            return true;
        }

        if (Providers.Any())
        {
            return await ProviderUserForOrgAsync(orgId);
        }

        return false;
    }

    public Task<bool> OrganizationCustom(Guid orgId)
    {
        return Task.FromResult(Organizations?.Any(o => o.Id == orgId && o.Type == OrganizationUserType.Custom) ?? false);
    }

    public async Task<bool> AccessEventLogs(Guid orgId)
    {
        return await OrganizationAdmin(orgId) || (Organizations?.Any(o => o.Id == orgId
                    && (o.Permissions?.AccessEventLogs ?? false)) ?? false);
    }

    public async Task<bool> AccessImportExport(Guid orgId)
    {
        return await OrganizationAdmin(orgId) || (Organizations?.Any(o => o.Id == orgId
                    && (o.Permissions?.AccessImportExport ?? false)) ?? false);
    }

    public async Task<bool> AccessReports(Guid orgId)
    {
        return await OrganizationAdmin(orgId) || (Organizations?.Any(o => o.Id == orgId
                    && (o.Permissions?.AccessReports ?? false)) ?? false);
    }

    public async Task<bool> CreateNewCollections(Guid orgId)
    {
        return await OrganizationManager(orgId) || (Organizations?.Any(o => o.Id == orgId
                    && (o.Permissions?.CreateNewCollections ?? false)) ?? false);
    }

    public async Task<bool> EditAnyCollection(Guid orgId)
    {
        return await OrganizationAdmin(orgId) || (Organizations?.Any(o => o.Id == orgId
                    && (o.Permissions?.EditAnyCollection ?? false)) ?? false);
    }

    public async Task<bool> DeleteAnyCollection(Guid orgId)
    {
        return await OrganizationAdmin(orgId) || (Organizations?.Any(o => o.Id == orgId
                    && (o.Permissions?.DeleteAnyCollection ?? false)) ?? false);
    }

    public async Task<bool> ViewAllCollections(Guid orgId)
    {
        return await CreateNewCollections(orgId) || await EditAnyCollection(orgId) || await DeleteAnyCollection(orgId);
    }

    public async Task<bool> EditAssignedCollections(Guid orgId)
    {
        return await OrganizationManager(orgId) || (Organizations?.Any(o => o.Id == orgId
                    && (o.Permissions?.EditAssignedCollections ?? false)) ?? false);
    }

    public async Task<bool> DeleteAssignedCollections(Guid orgId)
    {
        return await OrganizationManager(orgId) || (Organizations?.Any(o => o.Id == orgId
                    && (o.Permissions?.DeleteAssignedCollections ?? false)) ?? false);
    }

    public async Task<bool> ViewAssignedCollections(Guid orgId)
    {
        return await EditAssignedCollections(orgId) || await DeleteAssignedCollections(orgId);
    }

    public async Task<bool> ManageGroups(Guid orgId)
    {
        return await OrganizationAdmin(orgId) || (Organizations?.Any(o => o.Id == orgId
                    && (o.Permissions?.ManageGroups ?? false)) ?? false);
    }

    public async Task<bool> ManagePolicies(Guid orgId)
    {
        return await OrganizationAdmin(orgId) || (Organizations?.Any(o => o.Id == orgId
                    && (o.Permissions?.ManagePolicies ?? false)) ?? false);
    }

    public async Task<bool> ManageSso(Guid orgId)
    {
        return await OrganizationAdmin(orgId) || (Organizations?.Any(o => o.Id == orgId
                    && (o.Permissions?.ManageSso ?? false)) ?? false);
    }

    public async Task<bool> ManageScim(Guid orgId)
    {
        return await OrganizationAdmin(orgId) || (Organizations?.Any(o => o.Id == orgId
                    && (o.Permissions?.ManageScim ?? false)) ?? false);
    }

    public async Task<bool> ManageUsers(Guid orgId)
    {
        return await OrganizationAdmin(orgId) || (Organizations?.Any(o => o.Id == orgId
                    && (o.Permissions?.ManageUsers ?? false)) ?? false);
    }

    public async Task<bool> ManageResetPassword(Guid orgId)
    {
        return await OrganizationAdmin(orgId) || (Organizations?.Any(o => o.Id == orgId
                    && (o.Permissions?.ManageResetPassword ?? false)) ?? false);
    }

    public async Task<bool> ManageBilling(Guid orgId)
    {
        var orgManagedByProvider = await ProviderIdForOrg(orgId) != null;

        return orgManagedByProvider
            ? await ProviderUserForOrgAsync(orgId)
            : await OrganizationOwner(orgId);
    }

    public bool ProviderProviderAdmin(Guid providerId)
    {
        return Providers?.Any(o => o.Id == providerId && o.Type == ProviderUserType.ProviderAdmin) ?? false;
    }

    public bool ProviderManageUsers(Guid providerId)
    {
        return ProviderProviderAdmin(providerId);
    }

    public bool ProviderAccessEventLogs(Guid providerId)
    {
        return ProviderProviderAdmin(providerId);
    }

    public bool AccessProviderOrganizations(Guid providerId)
    {
        return ProviderUser(providerId);
    }

    public bool ManageProviderOrganizations(Guid providerId)
    {
        return ProviderProviderAdmin(providerId);
    }

    public bool ProviderUser(Guid providerId)
    {
        return Providers?.Any(o => o.Id == providerId) ?? false;
    }

    public async Task<bool> ProviderUserForOrgAsync(Guid orgId)
    {
        return (await GetProviderOrganizations()).Any(po => po.OrganizationId == orgId);
    }

    public async Task<Guid?> ProviderIdForOrg(Guid orgId)
    {
        if (Organizations?.Any(org => org.Id == orgId) ?? false)
        {
            return null;
        }

        var po = (await GetProviderOrganizations())
            ?.FirstOrDefault(po => po.OrganizationId == orgId);

        return po?.ProviderId;
    }

    public async Task<ICollection<CurrentContentOrganization>> OrganizationMembershipAsync(
        IOrganizationUserRepository organizationUserRepository, Guid userId)
    {
        if (Organizations == null)
        {
            var userOrgs = await organizationUserRepository.GetManyByUserAsync(userId);
            Organizations = userOrgs.Where(ou => ou.Status == OrganizationUserStatusType.Confirmed)
                .Select(ou => new CurrentContentOrganization(ou)).ToList();
        }
        return Organizations;
    }

    public async Task<ICollection<CurrentContentProvider>> ProviderMembershipAsync(
        IProviderUserRepository providerUserRepository, Guid userId)
    {
        if (Providers == null)
        {
            var userProviders = await providerUserRepository.GetManyByUserAsync(userId);
            Providers = userProviders.Where(ou => ou.Status == ProviderUserStatusType.Confirmed)
                .Select(ou => new CurrentContentProvider(ou)).ToList();
        }
        return Providers;
    }

    private string GetClaimValue(Dictionary<string, IEnumerable<Claim>> claims, string type)
    {
        if (!claims.ContainsKey(type))
        {
            return null;
        }

        return claims[type].FirstOrDefault()?.Value;
    }

    private Permissions SetOrganizationPermissionsFromClaims(string organizationId, Dictionary<string, IEnumerable<Claim>> claimsDict)
    {
        bool hasClaim(string claimKey)
        {
            return claimsDict.ContainsKey(claimKey) ?
                claimsDict[claimKey].Any(x => x.Value == organizationId) : false;
        }

        return new Permissions
        {
            AccessEventLogs = hasClaim("accesseventlogs"),
            AccessImportExport = hasClaim("accessimportexport"),
            AccessReports = hasClaim("accessreports"),
            CreateNewCollections = hasClaim("createnewcollections"),
            EditAnyCollection = hasClaim("editanycollection"),
            DeleteAnyCollection = hasClaim("deleteanycollection"),
            EditAssignedCollections = hasClaim("editassignedcollections"),
            DeleteAssignedCollections = hasClaim("deleteassignedcollections"),
            ManageGroups = hasClaim("managegroups"),
            ManagePolicies = hasClaim("managepolicies"),
            ManageSso = hasClaim("managesso"),
            ManageUsers = hasClaim("manageusers"),
            ManageResetPassword = hasClaim("manageresetpassword"),
            ManageScim = hasClaim("managescim"),
        };
    }

    protected async Task<IEnumerable<ProviderUserOrganizationDetails>> GetProviderOrganizations()
    {
        if (_providerUserOrganizations == null && UserId.HasValue)
        {
            _providerUserOrganizations = await _providerUserRepository.GetManyOrganizationDetailsByUserAsync(UserId.Value, ProviderUserStatusType.Confirmed);
        }

        return _providerUserOrganizations;
    }
}
