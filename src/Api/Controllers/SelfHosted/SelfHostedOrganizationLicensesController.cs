using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.OrganizationFeatures.OrganizationLicenses.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Bit.Core.Utilities;
using Bit.Core.Exceptions;

namespace Bit.Api.Controllers.SelfHosted;

[Route("organization/license/self-hosted")]
[Authorize("Application")]
[SelfHosted(SelfHostedOnly = true)]
public class SelfHostedOrganizationLicensesController
{
    private readonly ICurrentContext _currentContext;
    private readonly ISelfHostedGetOrganizationLicenseFromCloudQuery _selfHostedGetOrganizationLicenseFromCloudQuery;
    private readonly IOrganizationConnectionRepository _organizationConnectionRepository;
    private readonly IOrganizationService _organizationService;
        
    public SelfHostedOrganizationLicensesController(
        ICurrentContext currentContext,
        ISelfHostedGetOrganizationLicenseFromCloudQuery selfHostedGetOrganizationLicenseFromCloudQuery,
        IOrganizationConnectionRepository organizationConnectionRepository,
        IOrganizationService organizationService)
    {
        _currentContext = currentContext;
        _selfHostedGetOrganizationLicenseFromCloudQuery = selfHostedGetOrganizationLicenseFromCloudQuery;
        _organizationConnectionRepository = organizationConnectionRepository;
        _organizationService = organizationService;
    }
    
    [HttpPost("sync/{id}")]
    public async Task SyncLicenseAsync(string id)
    {
        var orgIdGuid = new Guid(id);
        if (!await _currentContext.OrganizationOwner(orgIdGuid))
        {
            throw new NotFoundException();
        }

        var billingSyncConnection =
            (await _organizationConnectionRepository.GetByOrganizationIdTypeAsync(orgIdGuid,
                OrganizationConnectionType.CloudBillingSync)).FirstOrDefault();
        if (billingSyncConnection == null)
        {
            throw new NotFoundException("Unable to get Cloud Billing Sync connection");
        }

        var license =
            await _selfHostedGetOrganizationLicenseFromCloudQuery.GetLicenseAsync(orgIdGuid, billingSyncConnection);

        // TODO: use new command here instead
        await _organizationService.UpdateLicenseAsync(orgIdGuid, license);
    }
}
