using System.Text.Json;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Licenses;
using Bit.Core.Billing.Licenses.Extensions;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Services;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;

namespace Bit.Core.Billing.Organizations.Commands;

public interface IUpdateOrganizationLicenseCommand
{
    Task UpdateLicenseAsync(SelfHostedOrganizationDetails selfHostedOrganization,
        OrganizationLicense license, Organization? currentOrganizationUsingLicenseKey);
}

public class UpdateOrganizationLicenseCommand : IUpdateOrganizationLicenseCommand
{
    private readonly ILicensingService _licensingService;
    private readonly IGlobalSettings _globalSettings;
    private readonly IOrganizationService _organizationService;
    private readonly IFeatureService _featureService;

    public UpdateOrganizationLicenseCommand(
        ILicensingService licensingService,
        IGlobalSettings globalSettings,
        IOrganizationService organizationService,
        IFeatureService featureService)
    {
        _licensingService = licensingService;
        _globalSettings = globalSettings;
        _organizationService = organizationService;
        _featureService = featureService;
    }

    public async Task UpdateLicenseAsync(SelfHostedOrganizationDetails selfHostedOrganization,
        OrganizationLicense license, Organization? currentOrganizationUsingLicenseKey)
    {
        if (currentOrganizationUsingLicenseKey != null && currentOrganizationUsingLicenseKey.Id != selfHostedOrganization.Id)
        {
            throw new BadRequestException("License is already in use by another organization.");
        }

        var claimsPrincipal = _licensingService.GetClaimsPrincipalFromLicense(license);

        // If the license has a Token (claims-based), extract all properties from claims BEFORE validation
        // This ensures that CanUseLicense validation has access to the correct values from claims
        // Otherwise, fall back to using the properties already on the license object (backward compatibility)
        if (claimsPrincipal != null)
        {
            license.Name = claimsPrincipal.GetValue<string>(OrganizationLicenseConstants.Name);
            license.BillingEmail = claimsPrincipal.GetValue<string>(OrganizationLicenseConstants.BillingEmail);
            license.BusinessName = claimsPrincipal.GetValue<string>(OrganizationLicenseConstants.BusinessName);
            license.PlanType = claimsPrincipal.GetValue<PlanType>(OrganizationLicenseConstants.PlanType);
            license.Seats = claimsPrincipal.GetValue<int?>(OrganizationLicenseConstants.Seats);
            license.MaxCollections = claimsPrincipal.GetValue<short?>(OrganizationLicenseConstants.MaxCollections);
            license.UsePolicies = claimsPrincipal.GetValue<bool>(OrganizationLicenseConstants.UsePolicies);
            license.UseSso = claimsPrincipal.GetValue<bool>(OrganizationLicenseConstants.UseSso);
            license.UseKeyConnector = claimsPrincipal.GetValue<bool>(OrganizationLicenseConstants.UseKeyConnector);
            license.UseScim = claimsPrincipal.GetValue<bool>(OrganizationLicenseConstants.UseScim);
            license.UseGroups = claimsPrincipal.GetValue<bool>(OrganizationLicenseConstants.UseGroups);
            license.UseDirectory = claimsPrincipal.GetValue<bool>(OrganizationLicenseConstants.UseDirectory);
            license.UseEvents = claimsPrincipal.GetValue<bool>(OrganizationLicenseConstants.UseEvents);
            license.UseTotp = claimsPrincipal.GetValue<bool>(OrganizationLicenseConstants.UseTotp);
            license.Use2fa = claimsPrincipal.GetValue<bool>(OrganizationLicenseConstants.Use2fa);
            license.UseApi = claimsPrincipal.GetValue<bool>(OrganizationLicenseConstants.UseApi);
            license.UseResetPassword = claimsPrincipal.GetValue<bool>(OrganizationLicenseConstants.UseResetPassword);
            license.Plan = claimsPrincipal.GetValue<string>(OrganizationLicenseConstants.Plan);
            license.SelfHost = claimsPrincipal.GetValue<bool>(OrganizationLicenseConstants.SelfHost);
            license.UsersGetPremium = claimsPrincipal.GetValue<bool>(OrganizationLicenseConstants.UsersGetPremium);
            license.UseCustomPermissions = claimsPrincipal.GetValue<bool>(OrganizationLicenseConstants.UseCustomPermissions);
            license.Enabled = claimsPrincipal.GetValue<bool>(OrganizationLicenseConstants.Enabled);
            license.Expires = claimsPrincipal.GetValue<DateTime?>(OrganizationLicenseConstants.Expires);
            license.LicenseKey = claimsPrincipal.GetValue<string>(OrganizationLicenseConstants.LicenseKey);
            license.UsePasswordManager = claimsPrincipal.GetValue<bool>(OrganizationLicenseConstants.UsePasswordManager);
            license.UseSecretsManager = claimsPrincipal.GetValue<bool>(OrganizationLicenseConstants.UseSecretsManager);
            license.SmSeats = claimsPrincipal.GetValue<int?>(OrganizationLicenseConstants.SmSeats);
            license.SmServiceAccounts = claimsPrincipal.GetValue<int?>(OrganizationLicenseConstants.SmServiceAccounts);
            license.UseRiskInsights = claimsPrincipal.GetValue<bool>(OrganizationLicenseConstants.UseRiskInsights);
            license.UseOrganizationDomains = claimsPrincipal.GetValue<bool>(OrganizationLicenseConstants.UseOrganizationDomains);
            license.UseAdminSponsoredFamilies = claimsPrincipal.GetValue<bool>(OrganizationLicenseConstants.UseAdminSponsoredFamilies);
            license.UseAutomaticUserConfirmation = claimsPrincipal.GetValue<bool>(OrganizationLicenseConstants.UseAutomaticUserConfirmation);
        }

        var canUse = license.CanUse(_globalSettings, _licensingService, claimsPrincipal, out var exception) &&
            selfHostedOrganization.CanUseLicense(license, out exception);

        if (!canUse)
        {
            throw new BadRequestException(exception);
        }

        await WriteLicenseFileAsync(selfHostedOrganization, license);
        await UpdateOrganizationAsync(selfHostedOrganization, license);
    }

    private async Task WriteLicenseFileAsync(Organization organization, OrganizationLicense license)
    {
        var dir = $"{_globalSettings.LicenseDirectory}/organization";
        Directory.CreateDirectory(dir);
        await using var fs = new FileStream(Path.Combine(dir, $"{organization.Id}.json"), FileMode.Create);
        await JsonSerializer.SerializeAsync(fs, license, JsonHelpers.Indented);
    }

    private async Task UpdateOrganizationAsync(SelfHostedOrganizationDetails selfHostedOrganizationDetails, OrganizationLicense license)
    {
        var organization = selfHostedOrganizationDetails.ToOrganization();

        organization.UpdateFromLicense(license, _featureService);

        await _organizationService.ReplaceAndUpdateCacheAsync(organization);
    }
}
