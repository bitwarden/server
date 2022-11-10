using System.Text.Json;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.OrganizationConnectionConfigs;
using Bit.Core.OrganizationFeatures.OrganizationLicenses.Interfaces;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;

namespace Bit.Core.OrganizationFeatures.OrganizationLicenses;

public class UpdateOrganizationLicenseCommand : IUpdateOrganizationLicenseCommand
{
    private readonly ILicensingService _licensingService;
    private readonly IGlobalSettings _globalSettings;
    private readonly IOrganizationService _organizationService;

    public UpdateOrganizationLicenseCommand(
        ILicensingService licensingService,
        IGlobalSettings globalSettings,
        IOrganizationService organizationService)
    {
        _licensingService = licensingService;
        _globalSettings = globalSettings;
        _organizationService = organizationService;
    }

    public async Task UpdateLicenseAsync(Organization organization, OrganizationLicense license, SsoConfig ssoConfig)
    {
        license.CanUse(_globalSettings, _licensingService);
        await ValidateLicenseForOrganizationAsync(organization, license);
        await WriteLicenseFileAsync(organization, license);
        await UpdateOrganizationAsync(organization, license);
    }

    private async Task ValidateLicenseForOrganizationAsync(Organization organization, OrganizationLicense license, 
        SsoConfig ssoConfig, IEnumerable<Policy> policies, IEnumerable<Collection> collections, IEnumerable<Group> groups,
        IEnumerable<OrganizationConnection> scimConnections, IEnumerable<OrganizationUserUserDetails> orgUsers,
        IEnumerable<Organization> enabledOrganizations)
    {
        // TODO: Consider getting by licenseKey in database query, rather than passing in all orgs
        if (enabledOrganizations.Any(o => o.LicenseKey.Equals(license.LicenseKey) && o.Id != organization.Id))
        {
            throw new BadRequestException("License is already in use by another organization.");
        }

        var occupiedSeats = orgUsers.Count(ou => ou.OccupiesOrganizationSeat);
        if (license.Seats.HasValue && occupiedSeats > license.Seats.Value)
        {
            throw new BadRequestException($"Your organization currently has {occupiedSeats} seats filled. " +
                $"Your new license only has ({license.Seats.Value}) seats. Remove some users.");
        }

        var collectionCount = collections.Count();
        if (license.MaxCollections.HasValue && collectionCount > license.MaxCollections.Value)
        {
            throw new BadRequestException($"Your organization currently has {collectionCount} collections. " +
                $"Your new license allows for a maximum of ({license.MaxCollections.Value}) collections. " +
                "Remove some collections.");
        }

        if (!license.UseGroups && organization.UseGroups && groups.Any())
        {
            throw new BadRequestException($"Your organization currently has {groups.Count()} groups. " +
                $"Your new license does not allow for the use of groups. Remove all groups.");
        }

        var enabledPolicyCount = policies.Count(p => p.Enabled);
        if (!license.UsePolicies && organization.UsePolicies && enabledPolicyCount > 0)
        {
            throw new BadRequestException($"Your organization currently has {enabledPolicyCount} enabled " +
                $"policies. Your new license does not allow for the use of policies. Disable all policies.");
        }

        if (!license.UseSso && organization.UseSso && ssoConfig is { Enabled: true })
        {
            throw new BadRequestException($"Your organization currently has a SSO configuration. " +
                $"Your new license does not allow for the use of SSO. Disable your SSO configuration.");
        }

        if (!license.UseKeyConnector && organization.UseKeyConnector && 
            ssoConfig != null && ssoConfig.GetData().KeyConnectorEnabled)
        {
            throw new BadRequestException($"Your organization currently has Key Connector enabled. " +
                $"Your new license does not allow for the use of Key Connector. Disable your Key Connector.");
        }

        if (!license.UseScim && organization.UseScim && 
            scimConnections != null && 
            scimConnections.Any(c => c.GetConfig<ScimConfig>() is {Enabled: true}))
        {
            throw new BadRequestException("Your new plan does not allow the SCIM feature. " +
                "Disable your SCIM configuration.");
        }

        if (!license.UseResetPassword && organization.UseResetPassword &&
            policies.Any(p => p.Type == PolicyType.ResetPassword && p.Enabled))
        {
            throw new BadRequestException("Your new license does not allow the Password Reset feature. "
                + "Disable your Password Reset policy.");
        }
    }

    private async Task WriteLicenseFileAsync(Organization organization, OrganizationLicense license)
    {
        var dir = $"{_globalSettings.LicenseDirectory}/organization";
        Directory.CreateDirectory(dir);
        await using var fs = new FileStream(Path.Combine(dir, $"{organization.Id}.json"), FileMode.Create);
        await JsonSerializer.SerializeAsync(fs, license, JsonHelpers.Indented);
    }

    private async Task UpdateOrganizationAsync(Organization organization, OrganizationLicense license)
    {
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
