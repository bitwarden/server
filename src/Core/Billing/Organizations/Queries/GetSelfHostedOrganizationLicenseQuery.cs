// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Models.Business;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Models.Api.OrganizationLicenses;
using Bit.Core.Models.OrganizationConnectionConfigs;
using Bit.Core.Services;
using Bit.Core.Settings;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Billing.Organizations.Queries;

public interface IGetSelfHostedOrganizationLicenseQuery
{
    Task<OrganizationLicense> GetLicenseAsync(Organization organization, OrganizationConnection billingSyncConnection);
}

public class GetSelfHostedOrganizationLicenseQuery : BaseIdentityClientService, IGetSelfHostedOrganizationLicenseQuery
{
    private readonly IGlobalSettings _globalSettings;

    public GetSelfHostedOrganizationLicenseQuery(IHttpClientFactory httpFactory, IGlobalSettings globalSettings, ILogger<GetSelfHostedOrganizationLicenseQuery> logger, ICurrentContext currentContext)
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
    }

    public async Task<OrganizationLicense> GetLicenseAsync(Organization organization, OrganizationConnection billingSyncConnection)
    {
        if (!_globalSettings.SelfHosted)
        {
            throw new BadRequestException("This action is only available for self-hosted.");
        }

        if (!_globalSettings.EnableCloudCommunication)
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
