#nullable enable

using Bit.Core.AdminConsole.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Services;

// ReSharper disable once CheckNamespace
namespace Bit.Core.Billing.SelfHostLicenses.OrganizationLicenses;

public class UpdateOrganizationLicenseCommand : IUpdateOrganizationLicenseCommand
{
    private readonly ILicensingService _licensingService;
    private readonly IOrganizationService _organizationService;

    public UpdateOrganizationLicenseCommand(
        ILicensingService licensingService,
        IOrganizationService organizationService)
    {
        _licensingService = licensingService;
        _organizationService = organizationService;
    }

    public async Task UpdateLicenseAsync(SelfHostedOrganizationDetails selfHostedOrganization,
        OrganizationLicense license, Organization? currentOrganizationUsingLicenseKey)
    {
        if (currentOrganizationUsingLicenseKey != null && currentOrganizationUsingLicenseKey.Id != selfHostedOrganization.Id)
        {
            throw new BadRequestException("License is already in use by another organization.");
        }

        await _licensingService.WriteLicenseToDiskAsync(selfHostedOrganization.Id, license);
        await UpdateOrganizationAsync(selfHostedOrganization, license);
    }

    private async Task UpdateOrganizationAsync(SelfHostedOrganizationDetails selfHostedOrganizationDetails, OrganizationLicense license)
    {
        var organization = selfHostedOrganizationDetails.ToOrganization();
        organization.UpdateFromLicense(license);

        await _organizationService.ReplaceAndUpdateCacheAsync(organization);
    }
}
