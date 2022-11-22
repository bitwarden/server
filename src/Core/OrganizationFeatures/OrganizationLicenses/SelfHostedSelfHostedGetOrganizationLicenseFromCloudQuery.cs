using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Models.Api.OrganizationLicenses;
using Bit.Core.Models.Business;
using Bit.Core.Models.OrganizationConnectionConfigs;
using Bit.Core.OrganizationFeatures.OrganizationLicenses.Interfaces;
using Bit.Core.Services;
using Bit.Core.Settings;
using Microsoft.Extensions.Logging;

namespace Bit.Core.OrganizationFeatures.OrganizationLicenses;

public class SelfHostedSelfHostedGetOrganizationLicenseFromCloudQuery : BaseIdentityClientService, ISelfHostedGetOrganizationLicenseFromCloudQuery
{
    private readonly IGlobalSettings _globalSettings;

    public SelfHostedSelfHostedGetOrganizationLicenseFromCloudQuery(IHttpClientFactory httpFactory, IGlobalSettings globalSettings, ILogger<SelfHostedSelfHostedGetOrganizationLicenseFromCloudQuery> logger)
        : base(
            httpFactory,
            globalSettings.Installation.ApiUri,
            globalSettings.Installation.IdentityUri,
            "api.installation",
            $"installation.{globalSettings.Installation.Id}",
            globalSettings.Installation.Key,
            logger)
    {
        _globalSettings = globalSettings;
    }

    public async Task<OrganizationLicense> GetLicenseAsync(Guid organizationId, OrganizationConnection billingSyncConnection)
    {
        if (!_globalSettings.SelfHosted)
        {
            throw new BadRequestException("This action is only available for self-hosted.");
        }

        if (!_globalSettings.EnableCloudCommunication)
        {
            throw new BadRequestException("Failed to sync instance with cloud - Cloud communication is disabled in global settings");
        }

        // TODO: reduce duplication with self-host sync command
        // TODO: extract to validation method on the object
        if (!billingSyncConnection.Enabled)
        {
            throw new BadRequestException($"Billing Sync Key disabled for organization {organizationId}");
        }
        if (string.IsNullOrWhiteSpace(billingSyncConnection.Config))
        {
            throw new BadRequestException($"No Billing Sync Key known for organization {organizationId}");
        }

        // TODO: extract to validation method on the object
        var billingSyncConfig = billingSyncConnection.GetConfig<BillingSyncConfig>();
        if (billingSyncConfig == null || string.IsNullOrWhiteSpace(billingSyncConfig.BillingSyncKey))
        {
            throw new BadRequestException($"Failed to get Billing Sync Key for organization {organizationId}");
        }

        // Send the request to cloud
        var cloudOrganizationId = billingSyncConfig.CloudOrganizationId;

        var response = await SendAsync<OrganizationLicenseSyncRequestModel, OrganizationLicense>(
            HttpMethod.Get, $"licenses/organization/sync/{cloudOrganizationId}", new OrganizationLicenseSyncRequestModel()
            {
                BillingSyncKey = billingSyncConfig.BillingSyncKey,
            }, true);

        if (response == null)
        {
            _logger.LogDebug("Organization License sync failed for '{OrgId}'", organizationId);
            throw new BadRequestException("Organization License sync failed");
        }

        return response;
    }
}
