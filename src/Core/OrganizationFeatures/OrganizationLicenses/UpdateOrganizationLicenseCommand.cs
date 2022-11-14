#nullable enable

using System.Text.Json;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.OrganizationConnectionConfigs;
using Bit.Core.OrganizationFeatures.OrganizationLicenses.Interfaces;
using Bit.Core.Models.Data.Organizations;
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

    public async Task UpdateLicenseAsync(Organization organization, OrganizationLicense license, 
        Organization? existingOrganization, OrganizationPlanUsage planUsage)
    {
        license.CanUse(_globalSettings, _licensingService);
        ValidateLicenseForOrganizationAsync(organization, license, existingOrganization, planUsage);
        
        await WriteLicenseFileAsync(organization, license);
        await UpdateOrganizationAsync(organization, license);
    }

    private void ValidateLicenseForOrganizationAsync(Organization organization, OrganizationLicense license, 
        Organization? existingOrganization, OrganizationPlanUsage planUsage)
    {
        if (existingOrganization != null && existingOrganization.Id != organization.Id)
        {
            throw new BadRequestException("License is already in use by another organization.");
        }

        var occupiedSeats = planUsage.OrganizationUsers.Count(ou => ou.OccupiesOrganizationSeat);
        if (license.Seats.HasValue && occupiedSeats > license.Seats.Value)
        {
            throw new BadRequestException($"Your organization currently has {occupiedSeats} seats filled. " +
                $"Your new license only has ({license.Seats.Value}) seats. Remove some users.");
        }

        if (license.MaxCollections.HasValue && planUsage.CollectionCount > license.MaxCollections.Value)
        {
            throw new BadRequestException($"Your organization currently has {planUsage.CollectionCount} collections. " +
                $"Your new license allows for a maximum of ({license.MaxCollections.Value}) collections. " +
                "Remove some collections.");
        }

        if (!license.UseGroups && organization.UseGroups && planUsage.GroupCount > 1)
        {
            throw new BadRequestException($"Your organization currently has {planUsage.GroupCount} groups. " +
                $"Your new license does not allow for the use of groups. Remove all groups.");
        }

        var enabledPolicyCount = planUsage.Policies.Count(p => p.Enabled);
        if (!license.UsePolicies && organization.UsePolicies && enabledPolicyCount > 0)
        {
            throw new BadRequestException($"Your organization currently has {enabledPolicyCount} enabled " +
                $"policies. Your new license does not allow for the use of policies. Disable all policies.");
        }

        if (!license.UseSso && organization.UseSso && planUsage.SsoConfig is { Enabled: true })
        {
            throw new BadRequestException($"Your organization currently has a SSO configuration. " +
                $"Your new license does not allow for the use of SSO. Disable your SSO configuration.");
        }

        if (!license.UseKeyConnector && organization.UseKeyConnector && 
            planUsage.SsoConfig != null && planUsage.SsoConfig.GetData().KeyConnectorEnabled)
        {
            throw new BadRequestException($"Your organization currently has Key Connector enabled. " +
                $"Your new license does not allow for the use of Key Connector. Disable your Key Connector.");
        }

        if (!license.UseScim && organization.UseScim && 
            planUsage.ScimConnections != null && 
            planUsage.ScimConnections.Any(c => c.GetConfig<ScimConfig>() is {Enabled: true}))
        {
            throw new BadRequestException("Your new plan does not allow the SCIM feature. " +
                "Disable your SCIM configuration.");
        }

        if (!license.UseResetPassword && organization.UseResetPassword &&
            planUsage.Policies.Any(p => p.Type == PolicyType.ResetPassword && p.Enabled))
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
