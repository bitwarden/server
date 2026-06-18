using Bit.Api.Models.Response;
using Bit.Api.Pam.Models.Request;
using Bit.Api.Pam.Models.Response;
using Bit.Core;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Utilities;
using Bit.Pam.OrganizationFeatures.Commands.Interfaces;
using Bit.Pam.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Pam.Controllers;

[Route("organizations/{orgId:guid}/access-rules")]
[Authorize("Application")]
[RequireFeature(FeatureFlagKeys.Pam)]
public class AccessRulesController(
    ICurrentContext currentContext,
    IAccessRuleRepository repository,
    ICreateAccessRuleCommand createCommand,
    IUpdateAccessRuleCommand updateCommand,
    IDeleteAccessRuleCommand deleteCommand)
    : Controller
{
    [HttpGet("")]
    public async Task<ListResponseModel<AccessRuleResponseModel>> GetAll(Guid orgId)
    {
        await EnsureMemberAsync(orgId);

        var rules = await repository.GetManyDetailsByOrganizationIdAsync(orgId);
        return new ListResponseModel<AccessRuleResponseModel>(
            rules.Select(rule => new AccessRuleResponseModel(rule)));
    }

    [HttpGet("{id:guid}")]
    public async Task<AccessRuleResponseModel> Get(Guid orgId, Guid id)
    {
        await EnsureMemberAsync(orgId);

        var rule = await repository.GetDetailsByIdAsync(id);
        if (rule is null || rule.OrganizationId != orgId)
        {
            throw new NotFoundException();
        }

        return new AccessRuleResponseModel(rule);
    }

    [HttpPost("")]
    public async Task<AccessRuleResponseModel> Post(Guid orgId, [FromBody] AccessRuleRequestModel model)
    {
        await EnsureAdminAsync(orgId);

        var rule = await createCommand.CreateAsync(model.ToAccessRule(orgId), model.Collections);
        return new AccessRuleResponseModel(rule);
    }

    [HttpPut("{id:guid}")]
    public async Task<AccessRuleResponseModel> Put(Guid orgId, Guid id, [FromBody] AccessRuleRequestModel model)
    {
        await EnsureAdminAsync(orgId);

        var rule = await updateCommand.UpdateAsync(orgId, id, model.ToAccessRule(orgId), model.Collections);
        return new AccessRuleResponseModel(rule);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid orgId, Guid id)
    {
        await EnsureAdminAsync(orgId);

        await deleteCommand.DeleteAsync(orgId, id);
        return NoContent();
    }

    private async Task EnsureMemberAsync(Guid orgId)
    {
        if (!await currentContext.OrganizationUser(orgId))
        {
            throw new NotFoundException();
        }
    }

    private async Task EnsureAdminAsync(Guid orgId)
    {
        if (!await currentContext.OrganizationAdmin(orgId) && !await currentContext.OrganizationOwner(orgId))
        {
            throw new NotFoundException();
        }
    }
}
