using Bit.Api.Models.Request;
using Bit.Api.Models.Response;
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
    private readonly IVerifyOrganizationDomainCommand _verifyOrganizationDomainCommand;
    private readonly IDeleteOrganizationDomainCommand _deleteOrganizationDomainCommand;
    private readonly IGetOrganizationDomainByIdQuery _getOrganizationDomainByIdQuery;
    private readonly IGetOrganizationDomainByOrganizationIdQuery _getOrganizationDomainByOrganizationIdQuery;
    private readonly ICurrentContext _currentContext;
    private readonly IOrganizationRepository _organizationRepository;

    public OrganizationDomainController(
        ICreateOrganizationDomainCommand createOrganizationDomainCommand,
        IVerifyOrganizationDomainCommand verifyOrganizationDomainCommand,
        IDeleteOrganizationDomainCommand deleteOrganizationDomainCommand,
        IGetOrganizationDomainByIdQuery getOrganizationDomainByIdQuery,
        IGetOrganizationDomainByOrganizationIdQuery getOrganizationDomainByOrganizationIdQuery,
        ICurrentContext currentContext,
        IOrganizationRepository organizationRepository)
    {
        _createOrganizationDomainCommand = createOrganizationDomainCommand;
        _verifyOrganizationDomainCommand = verifyOrganizationDomainCommand;
        _deleteOrganizationDomainCommand = deleteOrganizationDomainCommand;
        _getOrganizationDomainByIdQuery = getOrganizationDomainByIdQuery;
        _getOrganizationDomainByOrganizationIdQuery = getOrganizationDomainByOrganizationIdQuery;
        _currentContext = currentContext;
        _organizationRepository = organizationRepository;
    }

    [HttpGet]
    public async Task<ListResponseModel<OrganizationDomainResponseModel>> Get(string orgId)
    {
        var orgIdGuid = new Guid(orgId);
        await ValidateOrganizationAccessAsync(orgIdGuid);

        var domains = await _getOrganizationDomainByOrganizationIdQuery
            .GetDomainsByOrganizationId(orgIdGuid);
        var response = domains.Select(x => new OrganizationDomainResponseModel(x)).ToList();
        return new ListResponseModel<OrganizationDomainResponseModel>(response);
    }

    [HttpGet("{id}")]
    public async Task<OrganizationDomainResponseModel> Get(string orgId, string id)
    {
        var orgIdGuid = new Guid(orgId);
        var IdGuid = new Guid(id);
        await ValidateOrganizationAccessAsync(orgIdGuid);

        var domain = await _getOrganizationDomainByIdQuery.GetOrganizationDomainById(IdGuid);
        if (domain is null)
        {
            throw new NotFoundException();
        }

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
            DomainName = model.DomainName.ToLower()
        };

        var domain = await _createOrganizationDomainCommand.CreateAsync(organizationDomain);
        return new OrganizationDomainResponseModel(domain);
    }

    [HttpPost("{id}/verify")]
    public async Task<bool> Verify(string orgId, string id)
    {
        var orgIdGuid = new Guid(orgId);
        var idGuid = new Guid(id);
        await ValidateOrganizationAccessAsync(orgIdGuid);

        return await _verifyOrganizationDomainCommand.VerifyOrganizationDomain(idGuid);
    }

    [HttpDelete("{id}")]
    [HttpPost("{id}/remove")]
    public async Task RemoveDomain(string orgId, string id)
    {
        var orgIdGuid = new Guid(orgId);
        var idGuid = new Guid(id);
        await ValidateOrganizationAccessAsync(orgIdGuid);

        await _deleteOrganizationDomainCommand.DeleteAsync(idGuid);
    }

    private async Task ValidateOrganizationAccessAsync(Guid orgIdGuid)
    {
        if (!await _currentContext.ManageSso(orgIdGuid))
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
