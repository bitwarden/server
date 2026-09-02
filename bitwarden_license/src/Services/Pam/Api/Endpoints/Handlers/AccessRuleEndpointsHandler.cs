using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.HttpExtensions;
using Bit.Pam.Repositories;
using Bit.Services.Pam.Api.Models.Request;
using Bit.Services.Pam.Api.Models.Response;
using Bit.Services.Pam.OrganizationFeatures.Commands.Interfaces;

namespace Bit.Services.Pam.Api.Endpoints.Handlers;

/// <summary>
/// Handler for the <c>organizations/{orgId}/access-rules</c> resource. The Minimal API endpoints (see
/// <c>AccessRuleEndpoints</c>) resolve this handler from DI.
/// </summary>
/// <remarks>
/// Access to the organization is already settled by the time a handler runs — <c>AccessRuleEndpoints</c> authorizes
/// the group and the write endpoints through the standard authorization middleware. What is left here is resource
/// scoping: confirming a rule reached by ID actually belongs to the organization on the route.
/// </remarks>
public class AccessRuleEndpointsHandler(
    ICurrentContext currentContext,
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

    public async Task<AccessRuleResponseModel> Post(Guid orgId, AccessRuleRequestModel model)
    {
        var toCreate = model.ToAccessRule(orgId);
        toCreate.LastEditedBy = currentContext.UserId;
        var rule = await createCommand.CreateAsync(toCreate, model.Collections);
        return new AccessRuleResponseModel(rule);
    }

    public async Task<AccessRuleResponseModel> Put(Guid orgId, Guid id, AccessRuleRequestModel model)
    {
        var toUpdate = model.ToAccessRule(orgId);
        toUpdate.LastEditedBy = currentContext.UserId;
        var rule = await updateCommand.UpdateAsync(orgId, id, toUpdate, model.Collections);
        return new AccessRuleResponseModel(rule);
    }

    public async Task Delete(Guid orgId, Guid id)
    {
        await deleteCommand.DeleteAsync(orgId, id);
    }
}
