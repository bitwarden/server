using Bit.Api.Models.Request;
using Bit.Api.Models.Response.Organizations;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.OrganizationDomains.Interfaces;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Controllers;

[Route("organizations/{orgId}/domain")]
[Authorize("Application")]
public class OrganizationDomainController : Controller
{
    private readonly ICreateOrganizationDomainCommand _createOrganizationDomainCommand;
    private readonly IGetOrganizationDomainByIdQuery _getOrganizationDomainByIdQuery;
    private readonly ICurrentContext _currentContext;
    private readonly IOrganizationRepository _organizationRepository;

    public OrganizationDomainController(
        ICreateOrganizationDomainCommand createOrganizationDomainCommand,
        IGetOrganizationDomainByIdQuery getOrganizationDomainByIdQuery,
        ICurrentContext currentContext,
        IOrganizationRepository organizationRepository)
    {
        _createOrganizationDomainCommand = createOrganizationDomainCommand;
        _getOrganizationDomainByIdQuery = getOrganizationDomainByIdQuery;
        _currentContext = currentContext;
        _organizationRepository = organizationRepository;
    }

    [HttpGet("{domainId}")]
    public async Task<OrganizationDomainResponseModel> Get(string orgId, string domainId)
    {
        var orgIdGuid = new Guid(orgId);
        var domainIdGuid = new Guid(domainId);
        await ValidateOrganizationAccessAsync(orgIdGuid);

        var domain = await _getOrganizationDomainByIdQuery.GetOrganizationDomainById(domainIdGuid);
        return new OrganizationDomainResponseModel(domain);
    }

    [HttpPost("")]
    public async Task<OrganizationDomainResponseModel> Post(string orgId, [FromBody] OrganizationDomainRequestModel model)
    {
        var orgIdGuid = new Guid(orgId);
        await ValidateOrganizationAccessAsync(orgIdGuid);

        var organizationDomain = new OrganizationDomain
        {
            OrganizationId = orgIdGuid,
            Txt = model.Txt,
            DomainName = model.DomainName
        };

        var domain = await _createOrganizationDomainCommand.CreateAsync(organizationDomain);
        return new OrganizationDomainResponseModel(domain);
    }

    private async Task ValidateOrganizationAccessAsync(Guid orgIdGuid)
    {
        if (!await _currentContext.OrganizationOwner(orgIdGuid))
        {
            throw new UnauthorizedAccessException();
        }

        var organization = await _organizationRepository.GetByIdAsync(orgIdGuid);
        if (organization == null)
        {
            throw new NotFoundException();
        }
    }
}
