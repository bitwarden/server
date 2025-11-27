using System.Text.Json;
using Bit.Core.AdminConsole.Entities;
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

        // Verify hash FIRST to detect tampering with license file content before any other validation
        // This is critical because if the file is tampered, all subsequent validations are meaningless
        if (!string.IsNullOrWhiteSpace(license.Hash))
        {
            var computedHash = Convert.ToBase64String(license.ComputeHash());
            if (!computedHash.Equals(license.Hash, StringComparison.Ordinal))
            {
                throw new BadRequestException("License file has been tampered with (hash mismatch). The license file content does not match the original hash.");
            }
        }
        else
        {
            // If hash is missing, this is suspicious - old licenses might not have hashes, but new ones should
            // For now, we'll allow it but this could be tightened in the future
        }

        var claimsPrincipal = _licensingService.GetClaimsPrincipalFromLicense(license);
        var canUse = license.CanUse(_globalSettings, _licensingService, claimsPrincipal, out var exception) &&
            selfHostedOrganization.CanUseLicense(license, out exception);

        if (!canUse)
        {
            throw new BadRequestException(exception);
        }

        // Validate license data including expiration date to prevent tampering
        var organization = selfHostedOrganization.ToOrganization();
        if (!license.VerifyData(organization, claimsPrincipal, _globalSettings))
        {
            throw new BadRequestException("Invalid license data. The license file may have been tampered with.");
        }

        var useAutomaticUserConfirmation = claimsPrincipal?
            .GetValue<bool>(OrganizationLicenseConstants.UseAutomaticUserConfirmation) ?? false;

        selfHostedOrganization.UseAutomaticUserConfirmation = useAutomaticUserConfirmation;
        license.UseAutomaticUserConfirmation = useAutomaticUserConfirmation;

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
