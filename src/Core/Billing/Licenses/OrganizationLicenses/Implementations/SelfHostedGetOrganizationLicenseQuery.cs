using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Models.Api.OrganizationLicenses;
using Bit.Core.Models.OrganizationConnectionConfigs;
using Bit.Core.Services;
using Bit.Core.Settings;
using Microsoft.Extensions.Logging;

// ReSharper disable once CheckNamespace
namespace Bit.Core.Billing.Licenses.OrganizationLicenses;

public class SelfHostedGetOrganizationLicenseQuery(
    IHttpClientFactory httpFactory,
    IGlobalSettings globalSettings,
    ILogger<SelfHostedGetOrganizationLicenseQuery> logger)
    : BaseIdentityClientService(httpFactory,
        globalSettings.Installation.ApiUri,
        globalSettings.Installation.IdentityUri,
        "api.licensing",
        $"installation.{globalSettings.Installation.Id}",
        globalSettings.Installation.Key,
        logger), ISelfHostedGetOrganizationLicenseQuery
{
    public async Task<OrganizationLicense> GetLicenseAsync(Organization organization, OrganizationConnection billingSyncConnection)
    {
        if (!globalSettings.SelfHosted)
        {
            throw new BadRequestException("This action is only available for self-hosted.");
        }

        if (!globalSettings.EnableCloudCommunication)
        {
            throw new BadRequestException("Cloud communication is disabled in global settings");
        }

        if (!billingSyncConnection.Validate<BillingSyncConfig>(out var exception))
        {
            throw new BadRequestException(exception);
        }

        var billingSyncConfig = billingSyncConnection.GetConfig<BillingSyncConfig>();
        var cloudOrganizationId = billingSyncConfig.CloudOrganizationId;

        var response = await SendAsync<SelfHostedOrganizationLicenseRequestModel, OrganizationLicense>(
            HttpMethod.Get, $"licenses/organization/{cloudOrganizationId}", new SelfHostedOrganizationLicenseRequestModel()
            {
                BillingSyncKey = billingSyncConfig.BillingSyncKey,
                LicenseKey = organization.LicenseKey,
            }, true);

        if (response == null)
        {
            _logger.LogDebug("Organization License sync failed for '{OrgId}'", organization.Id);
            throw new BadRequestException("An error has occurred. Check your internet connection and ensure the billing token is correct.");
        }

        return response;
    }
}
