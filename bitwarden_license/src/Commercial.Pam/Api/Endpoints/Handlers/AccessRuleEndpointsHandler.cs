using Bit.Commercial.Pam.Api.Models.Request;
using Bit.Commercial.Pam.Api.Models.Response;
using Bit.Commercial.Pam.OrganizationFeatures.Commands.Interfaces;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.HttpExtensions;
using Bit.Pam.Repositories;

namespace Bit.Commercial.Pam.Api.Endpoints.Handlers;

/// <summary>
/// Handler for the <c>organizations/{orgId}/access-rules</c> resource. Holds the logic the
/// <c>AccessRulesController</c> previously hosted; the Minimal API endpoints (see <c>AccessRuleEndpoints</c>)
/// resolve this handler from DI.
/// </summary>
public class AccessRuleEndpointsHandler(
    ICurrentContext currentContext,
    IAccessRuleRepository repository,
    ICreateAccessRuleCommand createCommand,
    IUpdateAccessRuleCommand updateCommand,
    IDeleteAccessRuleCommand deleteCommand)
{
    public async Task<ListResponseModel<AccessRuleResponseModel>> GetAll(Guid orgId)
    {
        await EnsureMemberAsync(orgId);

        var rules = await repository.GetManyDetailsByOrganizationIdAsync(orgId);
        return new ListResponseModel<AccessRuleResponseModel>(
            rules.Select(rule => new AccessRuleResponseModel(rule)));
    }

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

    public async Task<AccessRuleResponseModel> Post(Guid orgId, AccessRuleRequestModel model)
    {
        await EnsureAdminAsync(orgId);

        var rule = await createCommand.CreateAsync(model.ToAccessRule(orgId), model.Collections);
        return new AccessRuleResponseModel(rule);
    }

    public async Task<AccessRuleResponseModel> Put(Guid orgId, Guid id, AccessRuleRequestModel model)
    {
        await EnsureAdminAsync(orgId);

        var rule = await updateCommand.UpdateAsync(orgId, id, model.ToAccessRule(orgId), model.Collections);
        return new AccessRuleResponseModel(rule);
    }

    public async Task Delete(Guid orgId, Guid id)
    {
        await EnsureAdminAsync(orgId);

        await deleteCommand.DeleteAsync(orgId, id);
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
