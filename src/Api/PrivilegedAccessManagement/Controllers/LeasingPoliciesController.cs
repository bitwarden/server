using Bit.Api.Models.Response;
using Bit.Api.PrivilegedAccessManagement.Models.Request;
using Bit.Api.PrivilegedAccessManagement.Models.Response;
using Bit.Core;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.PrivilegedAccessManagement.OrganizationFeatures.Commands.Interfaces;
using Bit.Core.PrivilegedAccessManagement.Repositories;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.PrivilegedAccessManagement.Controllers;

[Route("organizations/{orgId:guid}/leasing-policies")]
[Authorize("Application")]
[RequireFeature(FeatureFlagKeys.Pam)]
public class LeasingPoliciesController : Controller
{
    private readonly ICurrentContext _currentContext;
    private readonly ILeasingPolicyRepository _repository;
    private readonly ICreateLeasingPolicyCommand _createCommand;
    private readonly IUpdateLeasingPolicyCommand _updateCommand;
    private readonly IDeleteLeasingPolicyCommand _deleteCommand;

    public LeasingPoliciesController(
        ICurrentContext currentContext,
        ILeasingPolicyRepository repository,
        ICreateLeasingPolicyCommand createCommand,
        IUpdateLeasingPolicyCommand updateCommand,
        IDeleteLeasingPolicyCommand deleteCommand)
    {
        _currentContext = currentContext;
        _repository = repository;
        _createCommand = createCommand;
        _updateCommand = updateCommand;
        _deleteCommand = deleteCommand;
    }

    [HttpGet("")]
    public async Task<ListResponseModel<LeasingPolicyResponseModel>> GetAll(Guid orgId)
    {
        await EnsureMemberAsync(orgId);

        var policies = await _repository.GetManyByOrganizationIdAsync(orgId);
        return new ListResponseModel<LeasingPolicyResponseModel>(
            policies.Select(p => new LeasingPolicyResponseModel(p)));
    }

    [HttpGet("{id:guid}")]
    public async Task<LeasingPolicyResponseModel> Get(Guid orgId, Guid id)
    {
        await EnsureMemberAsync(orgId);

        var policy = await _repository.GetByIdAsync(id);
        if (policy is null || policy.OrganizationId != orgId)
        {
            throw new NotFoundException();
        }

        return new LeasingPolicyResponseModel(policy);
    }

    [HttpPost("")]
    public async Task<LeasingPolicyResponseModel> Post(Guid orgId, [FromBody] LeasingPolicyRequestModel model)
    {
        await EnsureAdminAsync(orgId);

        var policy = await _createCommand.CreateAsync(model.ToLeasingPolicy(orgId));
        return new LeasingPolicyResponseModel(policy);
    }

    [HttpPut("{id:guid}")]
    public async Task<LeasingPolicyResponseModel> Put(Guid orgId, Guid id, [FromBody] LeasingPolicyRequestModel model)
    {
        await EnsureAdminAsync(orgId);

        var policy = await _updateCommand.UpdateAsync(orgId, id, model.ToLeasingPolicy(orgId));
        return new LeasingPolicyResponseModel(policy);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid orgId, Guid id)
    {
        await EnsureAdminAsync(orgId);

        await _deleteCommand.DeleteAsync(orgId, id);
        return NoContent();
    }

    private async Task EnsureMemberAsync(Guid orgId)
    {
        if (!await _currentContext.OrganizationUser(orgId))
        {
            throw new NotFoundException();
        }
    }

    private async Task EnsureAdminAsync(Guid orgId)
    {
        if (!await _currentContext.OrganizationAdmin(orgId) && !await _currentContext.OrganizationOwner(orgId))
        {
            throw new NotFoundException();
        }
    }
}
