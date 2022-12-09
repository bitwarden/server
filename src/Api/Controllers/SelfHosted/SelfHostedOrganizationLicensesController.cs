using Bit.Api.Models.Request;
using Bit.Api.Models.Request.Organizations;
using Bit.Api.Models.Response.Organizations;
using Bit.Api.Utilities;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.OrganizationFeatures.OrganizationLicenses.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Controllers.SelfHosted;

[Route("organizations/licenses/self-hosted")]
[Authorize("Application")]
[SelfHosted(SelfHostedOnly = true)]
public class SelfHostedOrganizationLicensesController : Controller
{
    private readonly ICurrentContext _currentContext;
    private readonly ISelfHostedGetOrganizationLicenseQuery _selfHostedGetOrganizationLicenseQuery;
    private readonly IOrganizationConnectionRepository _organizationConnectionRepository;
    private readonly IOrganizationService _organizationService;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IUserService _userService;

    public SelfHostedOrganizationLicensesController(
        ICurrentContext currentContext,
        ISelfHostedGetOrganizationLicenseQuery selfHostedGetOrganizationLicenseQuery,
        IOrganizationConnectionRepository organizationConnectionRepository,
        IOrganizationService organizationService,
        IOrganizationRepository organizationRepository,
        IUserService userService)
    {
        _currentContext = currentContext;
        _selfHostedGetOrganizationLicenseQuery = selfHostedGetOrganizationLicenseQuery;
        _organizationConnectionRepository = organizationConnectionRepository;
        _organizationService = organizationService;
        _organizationRepository = organizationRepository;
        _userService = userService;
    }

    [HttpPost("")]
    public async Task<OrganizationResponseModel> PostLicenseAsync(OrganizationCreateLicenseRequestModel model)
    {
        var user = await _userService.GetUserByPrincipalAsync(User);
        if (user == null)
        {
            throw new UnauthorizedAccessException();
        }

        var license = await ApiHelpers.ReadJsonFileFromBody<OrganizationLicense>(HttpContext, model.License);
        if (license == null)
        {
            throw new BadRequestException("Invalid license");
        }

        var result = await _organizationService.SignUpAsync(license, user, model.Key,
            model.CollectionName, model.Keys?.PublicKey, model.Keys?.EncryptedPrivateKey);
        return new OrganizationResponseModel(result.Item1);
    }

    [HttpPost("{id}")]
    public async Task PostLicenseAsync(string id, LicenseRequestModel model)
    {
        var orgIdGuid = new Guid(id);
        if (!await _currentContext.OrganizationOwner(orgIdGuid))
        {
            throw new NotFoundException();
        }

        var license = await ApiHelpers.ReadJsonFileFromBody<OrganizationLicense>(HttpContext, model.License);
        if (license == null)
        {
            throw new BadRequestException("Invalid license");
        }

        await _organizationService.UpdateLicenseAsync(new Guid(id), license);
    }

    [HttpPost("{id}/sync")]
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
            await _selfHostedGetOrganizationLicenseQuery.GetLicenseAsync(organization, billingSyncConnection);

        await _organizationService.UpdateLicenseAsync(organization.Id, license);
    }
}
