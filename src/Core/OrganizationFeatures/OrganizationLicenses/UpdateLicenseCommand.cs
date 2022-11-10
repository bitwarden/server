using System.Text.Json;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.OrganizationConnectionConfigs;
using Bit.Core.Utilities;
using Bit.Core.OrganizationFeatures.OrganizationLicenses.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;

namespace Bit.Core.OrganizationFeatures.OrganizationLicenses;

public class UpdateLicenseCommand: IUpdateLicenseCommand
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ICollectionRepository _collectionRepository;
    private readonly IGroupRepository _groupRepository;
    private readonly ILicensingService _licensingService;
    private readonly IPolicyRepository _policyRepository;
    private readonly ISsoConfigRepository _ssoConfigRepository;
    private readonly IGlobalSettings _globalSettings;
    private readonly IOrganizationConnectionRepository _organizationConnectionRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IOrganizationService _organizationService;

    public UpdateLicenseCommand(
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IGroupRepository groupRepository,
        ILicensingService licensingService,
        ISsoConfigRepository ssoConfigRepository,
        IGlobalSettings globalSettings,
        IOrganizationConnectionRepository organizationConnectionRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationService organizationService)
    {
        _organizationRepository = organizationRepository;
        _collectionRepository = collectionRepository;
        _groupRepository = groupRepository;
        _licensingService = licensingService;
        _ssoConfigRepository = ssoConfigRepository;
        _globalSettings = globalSettings;
        _organizationConnectionRepository = organizationConnectionRepository;
        _organizationUserRepository = organizationUserRepository;
        _organizationService = organizationService;
    }
    
    public async Task UpdateLicenseAsync(Organization organization, OrganizationLicense license)
    {
        license.CanUse(_globalSettings, _licensingService);

        var enabledOrgs = await _organizationRepository.GetManyByEnabledAsync();
        if (enabledOrgs.Any(o => o.LicenseKey.Equals(license.LicenseKey) && o.Id != organization.Id))
        {
            throw new BadRequestException("License is already in use by another organization.");
        }

        if (license.Seats.HasValue &&
            (!organization.Seats.HasValue || organization.Seats.Value > license.Seats.Value))
        {
            var orgUsers = await _organizationUserRepository.GetManyDetailsByOrganizationAsync(organization.Id);
            var occupiedSeats  = orgUsers.Count(ou => ou.OccupiesOrganizationSeat);
            if (occupiedSeats > license.Seats.Value)
            {
                throw new BadRequestException($"Your organization currently has {occupiedSeats} seats filled. " +
                    $"Your new license only has ({license.Seats.Value}) seats. Remove some users.");
            }
        }

        if (license.MaxCollections.HasValue && (!organization.MaxCollections.HasValue ||
            organization.MaxCollections.Value > license.MaxCollections.Value))
        {
            var collectionCount = await _collectionRepository.GetCountByOrganizationIdAsync(organization.Id);
            if (collectionCount > license.MaxCollections.Value)
            {
                throw new BadRequestException($"Your organization currently has {collectionCount} collections. " +
                    $"Your new license allows for a maximum of ({license.MaxCollections.Value}) collections. " +
                    "Remove some collections.");
            }
        }

        if (!license.UseGroups && organization.UseGroups)
        {
            var groups = await _groupRepository.GetManyByOrganizationIdAsync(organization.Id);
            if (groups.Any())
            {
                throw new BadRequestException($"Your organization currently has {groups.Count} groups. " +
                    $"Your new license does not allow for the use of groups. Remove all groups.");
            }
        }

        if (!license.UsePolicies && organization.UsePolicies)
        {
            var policies = await _policyRepository.GetManyByOrganizationIdAsync(organization.Id);
            if (policies.Any(p => p.Enabled))
            {
                throw new BadRequestException($"Your organization currently has {policies.Count} enabled " +
                    $"policies. Your new license does not allow for the use of policies. Disable all policies.");
            }
        }

        if (!license.UseSso && organization.UseSso)
        {
            var ssoConfig = await _ssoConfigRepository.GetByOrganizationIdAsync(organization.Id);
            if (ssoConfig is { Enabled: true})
            {
                throw new BadRequestException($"Your organization currently has a SSO configuration. " +
                    $"Your new license does not allow for the use of SSO. Disable your SSO configuration.");
            }
        }

        if (!license.UseKeyConnector && organization.UseKeyConnector)
        {
            var ssoConfig = await _ssoConfigRepository.GetByOrganizationIdAsync(organization.Id);
            if (ssoConfig != null && ssoConfig.GetData().KeyConnectorEnabled)
            {
                throw new BadRequestException($"Your organization currently has Key Connector enabled. " +
                    $"Your new license does not allow for the use of Key Connector. Disable your Key Connector.");
            }
        }

        if (!license.UseScim && organization.UseScim)
        {
            var scimConnections = await _organizationConnectionRepository.GetByOrganizationIdTypeAsync(organization.Id,
                OrganizationConnectionType.Scim);
            if (scimConnections != null && scimConnections.Any(c => c.GetConfig<ScimConfig>()?.Enabled == true))
            {
                throw new BadRequestException("Your new plan does not allow the SCIM feature. " +
                    "Disable your SCIM configuration.");
            }
        }

        if (!license.UseResetPassword && organization.UseResetPassword)
        {
            var resetPasswordPolicy =
                await _policyRepository.GetByOrganizationIdTypeAsync(organization.Id, PolicyType.ResetPassword);
            if (resetPasswordPolicy is { Enabled: true })
            {
                throw new BadRequestException("Your new license does not allow the Password Reset feature. "
                    + "Disable your Password Reset policy.");
            }
        }

        var dir = $"{_globalSettings.LicenseDirectory}/organization";
        Directory.CreateDirectory(dir);
        await using var fs = new FileStream(Path.Combine(dir, $"{organization.Id}.json"), FileMode.Create);
        await JsonSerializer.SerializeAsync(fs, license, JsonHelpers.Indented);

        organization.Name = license.Name;
        organization.BusinessName = license.BusinessName;
        organization.BillingEmail = license.BillingEmail;
        organization.PlanType = license.PlanType;
        organization.Seats = license.Seats;
        organization.MaxCollections = license.MaxCollections;
        organization.UseGroups = license.UseGroups;
        organization.UseDirectory = license.UseDirectory;
        organization.UseEvents = license.UseEvents;
        organization.UseTotp = license.UseTotp;
        organization.Use2fa = license.Use2fa;
        organization.UseApi = license.UseApi;
        organization.UsePolicies = license.UsePolicies;
        organization.UseSso = license.UseSso;
        organization.UseKeyConnector = license.UseKeyConnector;
        organization.UseScim = license.UseScim;
        organization.UseResetPassword = license.UseResetPassword;
        organization.SelfHost = license.SelfHost;
        organization.UsersGetPremium = license.UsersGetPremium;
        organization.Plan = license.Plan;
        organization.Enabled = license.Enabled;
        organization.ExpirationDate = license.Expires;
        organization.LicenseKey = license.LicenseKey;
        organization.RevisionDate = DateTime.UtcNow;
        
        await _organizationService.ReplaceAndUpdateCacheAsync(organization);
    }
}
