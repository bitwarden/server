using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Licenses.Extensions;
using Bit.Core.Jobs;
using Bit.Core.Models.Business;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Quartz;

namespace Bit.Api.Jobs;

public class ValidateOrganizationsJob : BaseJob
{
    private readonly ILicensingService _licensingService;
    private readonly IGlobalSettings _globalSettings;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IMailService _mailService;

    public ValidateOrganizationsJob(
        ILicensingService licensingService,
        IGlobalSettings globalSettings,
        IOrganizationRepository organizationRepository,
        IMailService mailService,
        ILogger<ValidateOrganizationsJob> logger)
        : base(logger)
    {
        _licensingService = licensingService;
        _globalSettings = globalSettings;
        _organizationRepository = organizationRepository;
        _mailService = mailService;
    }

    protected async override Task ExecuteJobAsync(IJobExecutionContext context)
    {
        await ValidateOrganizationsAsync();
    }

    private async Task ValidateOrganizationsAsync()
    {
        if (!_globalSettings.SelfHosted)
        {
            return;
        }

        var enabledOrgs = await _organizationRepository.GetManyByEnabledAsync();

        _logger.LogInformation(Constants.BypassFiltersEventId, null,
            "Validating licenses for {NumberOfOrganizations} organizations.", enabledOrgs.Count);

        var exceptions = new List<Exception>();

        foreach (var org in enabledOrgs)
        {
            try
            {
                var license = await _licensingService.ReadOrganizationLicenseAsync(org);
                if (license == null)
                {
                    await DisableOrganizationAsync(org, null, "No license file.");
                    continue;
                }

                var totalLicensedOrgs = enabledOrgs.Count(o => string.Equals(o.LicenseKey, license.LicenseKey));
                if (totalLicensedOrgs > 1)
                {
                    await DisableOrganizationAsync(org, license, "Multiple organizations.");
                    continue;
                }

                if (!license.VerifyData(org, _licensingService.GetClaimsPrincipalFromLicense(license), _globalSettings))
                {
                    await DisableOrganizationAsync(org, license, "Invalid data.");
                    continue;
                }

                if (!_licensingService.VerifyLicense(license))
                {
                    await DisableOrganizationAsync(org, license, "Invalid signature.");
                    continue;
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        }

        if (exceptions.Count != 0)
        {
            throw new AggregateException("There were one or more exceptions while validating organizations.", exceptions);
        }
    }

    private async Task DisableOrganizationAsync(Organization org, ILicense license, string reason)
    {
        _logger.LogInformation(Constants.BypassFiltersEventId, null,
            "Organization {OrganizationId} ({OrganizationName}) has an invalid license and is being disabled. Reason: {Reason}",
            org.Id,
            org.DisplayName(),
            reason);

        org.Enabled = false;
        org.ExpirationDate = license?.Expires ?? DateTime.UtcNow;
        org.RevisionDate = DateTime.UtcNow;

        await _organizationRepository.ReplaceAsync(org);
        await _mailService.SendLicenseExpiredAsync([org.BillingEmail], org.DisplayName());
    }
}
