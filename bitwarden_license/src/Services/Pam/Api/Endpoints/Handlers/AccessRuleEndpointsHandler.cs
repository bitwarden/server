using System.Security.Claims;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bit.HttpExtensions;
using Bit.Pam.Repositories;
using Bit.Services.Pam.Api.Models.Request;
using Bit.Services.Pam.Api.Models.Response;
using Bit.Services.Pam.OrganizationFeatures.Commands.Interfaces;

namespace Bit.Services.Pam.Api.Endpoints.Handlers;

/// <summary>
/// Handler for the <c>organizations/{orgId}/access-rules</c> resource. Authorization runs in the middleware (see
/// <c>AccessRuleEndpoints</c>), so the handler and commands only check that a rule reached by id belongs to the
/// route's organization.
/// </summary>
public class AccessRuleEndpointsHandler(
    IUserService userService,
    IAccessRuleRepository repository,
    ICreateAccessRuleCommand createCommand,
    IUpdateAccessRuleCommand updateCommand,
    IDeleteAccessRuleCommand deleteCommand)
{
    public async Task<ListResponseModel<AccessRuleResponseModel>> GetAll(Guid orgId)
    {
        var rules = await repository.GetManyDetailsByOrganizationIdAsync(orgId);
        return new ListResponseModel<AccessRuleResponseModel>(
            rules.Select(rule => new AccessRuleResponseModel(rule)));
    }

    public async Task<AccessRuleResponseModel> Get(Guid orgId, Guid id)
    {
        var rule = await repository.GetDetailsByIdAsync(id);
        if (rule is null || rule.OrganizationId != orgId)
        {
            throw new NotFoundException();
        }

        return new AccessRuleResponseModel(rule);
    }

    public async Task<AccessRuleResponseModel> Post(ClaimsPrincipal user, Guid orgId, AccessRuleRequestModel model)
    {
        var toCreate = model.ToAccessRule(orgId);
        toCreate.LastEditedBy = userService.GetProperUserId(user)!.Value;
        var rule = await createCommand.CreateAsync(toCreate, model.Collections);
        return new AccessRuleResponseModel(rule);
    }

    public async Task<AccessRuleResponseModel> Put(ClaimsPrincipal user, Guid orgId, Guid id, AccessRuleRequestModel model)
    {
        var toUpdate = model.ToAccessRule(orgId);
        toUpdate.LastEditedBy = userService.GetProperUserId(user)!.Value;
        var rule = await updateCommand.UpdateAsync(orgId, id, toUpdate, model.Collections);
        return new AccessRuleResponseModel(rule);
    }

    public async Task Delete(ClaimsPrincipal user, Guid orgId, Guid id)
    {
        await deleteCommand.DeleteAsync(orgId, id, userService.GetProperUserId(user)!.Value);
    }
}
