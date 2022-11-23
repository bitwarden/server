using Bit.Core.Context;
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

public class SelfHostedGetOrganizationLicenseFromCloudQuery : BaseIdentityClientService, ISelfHostedGetOrganizationLicenseFromCloudQuery
{
    private readonly IGlobalSettings _globalSettings;
    private readonly ICurrentContext _currentContext;

    public SelfHostedGetOrganizationLicenseFromCloudQuery(IHttpClientFactory httpFactory, IGlobalSettings globalSettings, ILogger<SelfHostedGetOrganizationLicenseFromCloudQuery> logger, ICurrentContext currentContext)
        : base(
            httpFactory,
            globalSettings.Installation.ApiUri,
            globalSettings.Installation.IdentityUri,
            "api.licensing",
            $"installation.{globalSettings.Installation.Id}",
            globalSettings.Installation.Key,
            logger)
    {
        _globalSettings = globalSettings;
        _currentContext = currentContext;
    }

    public async Task<OrganizationLicense> GetLicenseAsync(Organization organization, OrganizationConnection billingSyncConnection)
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
            throw new BadRequestException($"Billing Sync Key disabled for organization {organization.Id}");
        }
        if (string.IsNullOrWhiteSpace(billingSyncConnection.Config))
        {
            throw new BadRequestException($"No Billing Sync Key known for organization {organization.Id}");
        }

        // TODO: extract to validation method on the object
        var billingSyncConfig = billingSyncConnection.GetConfig<BillingSyncConfig>();
        if (billingSyncConfig == null || string.IsNullOrWhiteSpace(billingSyncConfig.BillingSyncKey))
        {
            throw new BadRequestException($"Failed to get Billing Sync Key for organization {organization.Id}");
        }

        // Send the request to cloud
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
            throw new BadRequestException("Organization License sync failed");
        }

        return response;
    }
}
