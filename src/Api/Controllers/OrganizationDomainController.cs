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
    private readonly ICurrentContext _currentContext;
    private readonly IOrganizationRepository _organizationRepository;

    public OrganizationDomainController(
        ICreateOrganizationDomainCommand createOrganizationDomainCommand,
        ICurrentContext currentContext,
        IOrganizationRepository organizationRepository)
    {
        _createOrganizationDomainCommand = createOrganizationDomainCommand;
        _currentContext = currentContext;
        _organizationRepository = organizationRepository;
    }
    
    [HttpPost("")]
    public async Task<OrganizationDomainResponseModel> Post(string orgId, [FromBody] OrganizationDomainRequestModel model)
    {
        var orgIdGuid = new Guid(orgId);
        if (!await _currentContext.OrganizationOwner(orgIdGuid))
        {
            throw new UnauthorizedAccessException();
        }
        
        var organization = await _organizationRepository.GetByIdAsync(orgIdGuid);
        if (organization == null)
        {
            throw new NotFoundException();
        }

        var organizationDomain = new OrganizationDomain
        {
            OrganizationId = orgIdGuid, Txt = model.Txt, DomainName = model.DomainName
        };

        var domain = await _createOrganizationDomainCommand.CreateAsync(organizationDomain);
        return new OrganizationDomainResponseModel(domain);
    }
}
