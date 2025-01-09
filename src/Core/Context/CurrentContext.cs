using System.Security.Claims;
using Bit.Core.AdminConsole.Context;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Models.Data.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Extensions;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Identity;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Http;

namespace Bit.Core.Context;

public class CurrentContext : ICurrentContext
{
    private readonly IProviderOrganizationRepository _providerOrganizationRepository;
    private readonly IProviderUserRepository _providerUserRepository;
    private bool _builtHttpContext;
    private bool _builtClaimsPrincipal;
    private IEnumerable<ProviderOrganizationProviderDetails> _providerOrganizationProviderDetails;
    private IEnumerable<ProviderUserOrganizationDetails> _providerUserOrganizations;

    public virtual HttpContext HttpContext { get; set; }
    public virtual Guid? UserId { get; set; }
    public virtual User User { get; set; }
    public virtual string DeviceIdentifier { get; set; }
    public virtual DeviceType? DeviceType { get; set; }
    public virtual string IpAddress { get; set; }
    public virtual List<CurrentContextOrganization> Organizations { get; set; }
    public virtual List<CurrentContextProvider> Providers { get; set; }
    public virtual Guid? InstallationId { get; set; }
    public virtual Guid? OrganizationId { get; set; }
    public virtual bool CloudflareWorkerProxied { get; set; }
    public virtual bool IsBot { get; set; }
    public virtual bool MaybeBot { get; set; }
    public virtual int? BotScore { get; set; }
    public virtual string ClientId { get; set; }
    public virtual Version ClientVersion { get; set; }
    public virtual bool ClientVersionIsPrerelease { get; set; }
    public virtual IdentityClientType IdentityClientType { get; set; }
    public virtual Guid? ServiceAccountOrganizationId { get; set; }

    public CurrentContext(
        IProviderOrganizationRepository providerOrganizationRepository,
        IProviderUserRepository providerUserRepository)
    {
        _providerOrganizationRepository = providerOrganizationRepository;
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

        if (httpContext.Request.Headers.ContainsKey("Bitwarden-Client-Version") && Version.TryParse(httpContext.Request.Headers["Bitwarden-Client-Version"], out var cVersion))
        {
            ClientVersion = cVersion;
        }

        if (httpContext.Request.Headers.TryGetValue("Is-Prerelease", out var clientVersionIsPrerelease))
        {
            ClientVersionIsPrerelease = clientVersionIsPrerelease == "1";
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

        var clientType = GetClaimValue(claimsDict, Claims.Type);
        if (clientType != null)
        {
            Enum.TryParse(clientType, out IdentityClientType c);
            IdentityClientType = c;
        }

        if (IdentityClientType == IdentityClientType.ServiceAccount)
        {
            ServiceAccountOrganizationId = new Guid(GetClaimValue(claimsDict, Claims.Organization));
        }

        DeviceIdentifier = GetClaimValue(claimsDict, Claims.Device);

        Organizations = GetOrganizations(claimsDict, orgApi);

        Providers = GetProviders(claimsDict);

        return Task.FromResult(0);
    }

    private List<CurrentContextOrganization> GetOrganizations(Dictionary<string, IEnumerable<Claim>> claimsDict, bool orgApi)
    {
        var accessSecretsManager = claimsDict.ContainsKey(Claims.SecretsManagerAccess)
            ? claimsDict[Claims.SecretsManagerAccess].ToDictionary(s => s.Value, _ => true)
            : new Dictionary<string, bool>();

        var organizations = new List<CurrentContextOrganization>();
        if (claimsDict.ContainsKey(Claims.OrganizationOwner))
        {
            organizations.AddRange(claimsDict[Claims.OrganizationOwner].Select(c =>
                new CurrentContextOrganization
                {
                    Id = new Guid(c.Value),
                    Type = OrganizationUserType.Owner,
                    AccessSecretsManager = accessSecretsManager.ContainsKey(c.Value),
                }));
        }
        else if (orgApi && OrganizationId.HasValue)
        {
            organizations.Add(new CurrentContextOrganization
            {
                Id = OrganizationId.Value,
                Type = OrganizationUserType.Owner,
            });
        }

        if (claimsDict.ContainsKey(Claims.OrganizationAdmin))
        {
            organizations.AddRange(claimsDict[Claims.OrganizationAdmin].Select(c =>
                new CurrentContextOrganization
                {
                    Id = new Guid(c.Value),
                    Type = OrganizationUserType.Admin,
                    AccessSecretsManager = accessSecretsManager.ContainsKey(c.Value),
                }));
        }

        if (claimsDict.ContainsKey(Claims.OrganizationUser))
        {
            organizations.AddRange(claimsDict[Claims.OrganizationUser].Select(c =>
                new CurrentContextOrganization
                {
                    Id = new Guid(c.Value),
                    Type = OrganizationUserType.User,
                    AccessSecretsManager = accessSecretsManager.ContainsKey(c.Value),
                }));
        }

        if (claimsDict.ContainsKey(Claims.OrganizationCustom))
        {
            organizations.AddRange(claimsDict[Claims.OrganizationCustom].Select(c =>
                new CurrentContextOrganization
                {
                    Id = new Guid(c.Value),
                    Type = OrganizationUserType.Custom,
                    Permissions = SetOrganizationPermissionsFromClaims(c.Value, claimsDict),
                    AccessSecretsManager = accessSecretsManager.ContainsKey(c.Value),
                }));
        }

        return organizations;
    }

    private List<CurrentContextProvider> GetProviders(Dictionary<string, IEnumerable<Claim>> claimsDict)
    {
        var providers = new List<CurrentContextProvider>();
        if (claimsDict.ContainsKey(Claims.ProviderAdmin))
        {
            providers.AddRange(claimsDict[Claims.ProviderAdmin].Select(c =>
                new CurrentContextProvider
                {
                    Id = new Guid(c.Value),
                    Type = ProviderUserType.ProviderAdmin
                }));
        }

        if (claimsDict.ContainsKey(Claims.ProviderServiceUser))
        {
            providers.AddRange(claimsDict[Claims.ProviderServiceUser].Select(c =>
                new CurrentContextProvider
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

    public async Task<bool> EditAnyCollection(Guid orgId)
    {
        return await OrganizationAdmin(orgId) || (Organizations?.Any(o => o.Id == orgId
                    && (o.Permissions?.EditAnyCollection ?? false)) ?? false);
    }

    public async Task<bool> ViewAllCollections(Guid orgId)
    {
        var org = GetOrganization(orgId);
        return await EditAnyCollection(orgId) || (org != null && org.Permissions.DeleteAnyCollection);
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

    public async Task<bool> ViewSubscription(Guid orgId)
    {
        var isManagedByBillableProvider = (await GetOrganizationProviderDetails()).Any(po => po.OrganizationId == orgId && po.ProviderType.SupportsConsolidatedBilling());

        return isManagedByBillableProvider
            ? await ProviderUserForOrgAsync(orgId)
            : await OrganizationOwner(orgId);
    }

    public async Task<bool> EditSubscription(Guid orgId)
    {
        var orgManagedByProvider = (await GetOrganizationProviderDetails()).Any(po => po.OrganizationId == orgId);

        return orgManagedByProvider
            ? await ProviderUserForOrgAsync(orgId)
            : await OrganizationOwner(orgId);
    }

    public async Task<bool> EditPaymentMethods(Guid orgId)
    {
        return await EditSubscription(orgId);
    }

    public async Task<bool> ViewBillingHistory(Guid orgId)
    {
        return await EditSubscription(orgId);
    }

    public async Task<bool> AccessMembersTab(Guid orgId)
    {
        return await OrganizationAdmin(orgId) || await ManageUsers(orgId) || await ManageResetPassword(orgId);
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
        return (await GetProviderUserOrganizations()).Any(po => po.OrganizationId == orgId);
    }

    public async Task<Guid?> ProviderIdForOrg(Guid orgId)
    {
        if (Organizations?.Any(org => org.Id == orgId) ?? false)
        {
            return null;
        }

        var po = (await GetProviderUserOrganizations())
            ?.FirstOrDefault(po => po.OrganizationId == orgId);

        return po?.ProviderId;
    }

    public bool AccessSecretsManager(Guid orgId)
    {
        if (ServiceAccountOrganizationId.HasValue && ServiceAccountOrganizationId.Value == orgId)
        {
            return true;
        }

        return Organizations?.Any(o => o.Id == orgId && o.AccessSecretsManager) ?? false;
    }

    public async Task<ICollection<CurrentContextOrganization>> OrganizationMembershipAsync(
        IOrganizationUserRepository organizationUserRepository, Guid userId)
    {
        if (Organizations == null)
        {
            // If we haven't had our user id set, take the one passed in since we are about to get information
            // for them anyways.
            UserId ??= userId;

            var userOrgs = await organizationUserRepository.GetManyDetailsByUserAsync(userId);
            Organizations = userOrgs.Where(ou => ou.Status == OrganizationUserStatusType.Confirmed)
                .Select(ou => new CurrentContextOrganization(ou)).ToList();
        }
        return Organizations;
    }

    public async Task<ICollection<CurrentContextProvider>> ProviderMembershipAsync(
        IProviderUserRepository providerUserRepository, Guid userId)
    {
        if (Providers == null)
        {
            // If we haven't had our user id set, take the one passed in since we are about to get information
            // for them anyways.
            UserId ??= userId;

            var userProviders = await providerUserRepository.GetManyByUserAsync(userId);
            Providers = userProviders.Where(ou => ou.Status == ProviderUserStatusType.Confirmed)
                .Select(ou => new CurrentContextProvider(ou)).ToList();
        }
        return Providers;
    }

    public CurrentContextOrganization GetOrganization(Guid orgId)
    {
        return Organizations?.Find(o => o.Id == orgId);
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
            ManageGroups = hasClaim("managegroups"),
            ManagePolicies = hasClaim("managepolicies"),
            ManageSso = hasClaim("managesso"),
            ManageUsers = hasClaim("manageusers"),
            ManageResetPassword = hasClaim("manageresetpassword"),
            ManageScim = hasClaim("managescim"),
        };
    }

    protected async Task<IEnumerable<ProviderUserOrganizationDetails>> GetProviderUserOrganizations()
    {
        if (_providerUserOrganizations == null && UserId.HasValue)
        {
            _providerUserOrganizations = await _providerUserRepository.GetManyOrganizationDetailsByUserAsync(UserId.Value, ProviderUserStatusType.Confirmed);
        }

        return _providerUserOrganizations;
    }

    protected async Task<IEnumerable<ProviderOrganizationProviderDetails>> GetOrganizationProviderDetails()
    {
        if (_providerOrganizationProviderDetails == null && UserId.HasValue)
        {
            _providerOrganizationProviderDetails = await _providerOrganizationRepository.GetManyByUserAsync(UserId.Value);
        }

        return _providerOrganizationProviderDetails;
    }
}
