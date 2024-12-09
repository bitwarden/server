using Bit.Api.AdminConsole.Models.Request;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.AdminConsole.Models.Response.Organizations;
using Bit.Api.Models.Response;
using Bit.Core;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationDomains.Interfaces;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.AdminConsole.Controllers;

[Route("organizations")]
[Authorize("Application")]
public class OrganizationDomainController : Controller
{
    private readonly ICreateOrganizationDomainCommand _createOrganizationDomainCommand;
    private readonly IVerifyOrganizationDomainCommand _verifyOrganizationDomainCommand;
    private readonly IDeleteOrganizationDomainCommand _deleteOrganizationDomainCommand;
    private readonly IGetOrganizationDomainByIdOrganizationIdQuery _getOrganizationDomainByIdAndOrganizationIdQuery;
    private readonly IGetOrganizationDomainByOrganizationIdQuery _getOrganizationDomainByOrganizationIdQuery;
    private readonly ICurrentContext _currentContext;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationDomainRepository _organizationDomainRepository;

    public OrganizationDomainController(
        ICreateOrganizationDomainCommand createOrganizationDomainCommand,
        IVerifyOrganizationDomainCommand verifyOrganizationDomainCommand,
        IDeleteOrganizationDomainCommand deleteOrganizationDomainCommand,
        IGetOrganizationDomainByIdOrganizationIdQuery getOrganizationDomainByIdAndOrganizationIdQuery,
        IGetOrganizationDomainByOrganizationIdQuery getOrganizationDomainByOrganizationIdQuery,
        ICurrentContext currentContext,
        IOrganizationRepository organizationRepository,
        IOrganizationDomainRepository organizationDomainRepository)
    {
        _createOrganizationDomainCommand = createOrganizationDomainCommand;
        _verifyOrganizationDomainCommand = verifyOrganizationDomainCommand;
        _deleteOrganizationDomainCommand = deleteOrganizationDomainCommand;
        _getOrganizationDomainByIdAndOrganizationIdQuery = getOrganizationDomainByIdAndOrganizationIdQuery;
        _getOrganizationDomainByOrganizationIdQuery = getOrganizationDomainByOrganizationIdQuery;
        _currentContext = currentContext;
        _organizationRepository = organizationRepository;
        _organizationDomainRepository = organizationDomainRepository;
    }

    [HttpGet("{orgId}/domain")]
    public async Task<ListResponseModel<OrganizationDomainResponseModel>> Get(Guid orgId)
    {
        await ValidateOrganizationAccessAsync(orgId);

        var domains = await _getOrganizationDomainByOrganizationIdQuery
            .GetDomainsByOrganizationIdAsync(orgId);
        var response = domains.Select(x => new OrganizationDomainResponseModel(x)).ToList();
        return new ListResponseModel<OrganizationDomainResponseModel>(response);
    }

    [HttpGet("{orgId}/domain/{id}")]
    public async Task<OrganizationDomainResponseModel> Get(Guid orgId, Guid id)
    {
        await ValidateOrganizationAccessAsync(orgId);

        var organizationDomain = await _getOrganizationDomainByIdAndOrganizationIdQuery
            .GetOrganizationDomainByIdOrganizationIdAsync(id, orgId);
        if (organizationDomain is null)
        {
            throw new NotFoundException();
        }

        return new OrganizationDomainResponseModel(organizationDomain);
    }

    [HttpPost("{orgId}/domain")]
    public async Task<OrganizationDomainResponseModel> Post(Guid orgId,
        [FromBody] OrganizationDomainRequestModel model)
    {
        await ValidateOrganizationAccessAsync(orgId);

        var organizationDomain = new OrganizationDomain
        {
            OrganizationId = orgId,
            DomainName = model.DomainName.ToLower()
        };

        organizationDomain = await _createOrganizationDomainCommand.CreateAsync(organizationDomain);

        return new OrganizationDomainResponseModel(organizationDomain);
    }

    [HttpPost("{orgId}/domain/{id}/verify")]
    public async Task<OrganizationDomainResponseModel> Verify(Guid orgId, Guid id)
    {
        await ValidateOrganizationAccessAsync(orgId);

        var organizationDomain = await _organizationDomainRepository.GetDomainByIdOrganizationIdAsync(id, orgId);
        if (organizationDomain is null)
        {
            throw new NotFoundException();
        }

        organizationDomain = await _verifyOrganizationDomainCommand.UserVerifyOrganizationDomainAsync(organizationDomain);

        return new OrganizationDomainResponseModel(organizationDomain);
    }

    [HttpDelete("{orgId}/domain/{id}")]
    [HttpPost("{orgId}/domain/{id}/remove")]
    public async Task RemoveDomain(Guid orgId, Guid id)
    {
        await ValidateOrganizationAccessAsync(orgId);

        var domain = await _organizationDomainRepository.GetDomainByIdOrganizationIdAsync(id, orgId);
        if (domain is null)
        {
            throw new NotFoundException();
        }

        await _deleteOrganizationDomainCommand.DeleteAsync(domain);
    }

    [AllowAnonymous]
    [HttpPost("domain/sso/details")] // must be post to accept email cleanly
    public async Task<OrganizationDomainSsoDetailsResponseModel> GetOrgDomainSsoDetails(
        [FromBody] OrganizationDomainSsoDetailsRequestModel model)
    {
        var ssoResult = await _organizationDomainRepository.GetOrganizationDomainSsoDetailsAsync(model.Email);
        if (ssoResult is null)
        {
            throw new NotFoundException("Claimed org domain not found");
        }

        return new OrganizationDomainSsoDetailsResponseModel(ssoResult);
    }

    [AllowAnonymous]
    [HttpPost("domain/sso/verified")]
    [RequireFeature(FeatureFlagKeys.VerifiedSsoDomainEndpoint)]
    public async Task<VerifiedOrganizationDomainSsoDetailsResponseModel> GetVerifiedOrgDomainSsoDetailsAsync(
        [FromBody] OrganizationDomainSsoDetailsRequestModel model)
    {
        var ssoResults = (await _organizationDomainRepository
            .GetVerifiedOrganizationDomainSsoDetailsAsync(model.Email))
            .ToList();

        return new VerifiedOrganizationDomainSsoDetailsResponseModel(
            ssoResults.Select(ssoResult => new VerifiedOrganizationDomainSsoDetailResponseModel(ssoResult)));
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
