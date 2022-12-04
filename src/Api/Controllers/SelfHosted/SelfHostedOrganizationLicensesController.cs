using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.OrganizationLicenses.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
    private readonly IOrganizationRepository _organizationRepository;

    public SelfHostedOrganizationLicensesController(
        ICurrentContext currentContext,
        ISelfHostedGetOrganizationLicenseFromCloudQuery selfHostedGetOrganizationLicenseFromCloudQuery,
        IOrganizationConnectionRepository organizationConnectionRepository,
        IOrganizationService organizationService,
        IOrganizationRepository organizationRepository)
    {
        _currentContext = currentContext;
        _selfHostedGetOrganizationLicenseFromCloudQuery = selfHostedGetOrganizationLicenseFromCloudQuery;
        _organizationConnectionRepository = organizationConnectionRepository;
        _organizationService = organizationService;
        _organizationRepository = organizationRepository;
    }

    [HttpPost("sync/{id}")]
    public async Task SyncLicenseAsync(string id)
    {
        var organization = await _organizationRepository.GetByIdAsync(new Guid(id));
        if (organization == null)
        {
            throw new NotFoundException();
        }

        if (!await _currentContext.OrganizationOwner(organization.Id))
        {
            throw new NotFoundException();
        }

        var billingSyncConnection =
            (await _organizationConnectionRepository.GetByOrganizationIdTypeAsync(organization.Id,
                OrganizationConnectionType.CloudBillingSync)).FirstOrDefault();
        if (billingSyncConnection == null)
        {
            throw new NotFoundException("Unable to get Cloud Billing Sync connection");
        }

        var license =
            await _selfHostedGetOrganizationLicenseFromCloudQuery.GetLicenseAsync(organization, billingSyncConnection);

        await _organizationService.UpdateLicenseAsync(organization.Id, license);
    }
}
