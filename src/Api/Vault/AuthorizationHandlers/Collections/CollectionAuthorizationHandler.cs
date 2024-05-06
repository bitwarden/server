#nullable enable
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Api.Vault.AuthorizationHandlers.Collections;

/// <summary>
/// Handles authorization logic for Collection operations.
/// This uses new logic implemented in the Flexible Collections initiative.
/// </summary>
public class CollectionAuthorizationHandler : AuthorizationHandler<CollectionOperationRequirement>
{
    private readonly ICurrentContext _currentContext;
    private readonly IFeatureService _featureService;

    public CollectionAuthorizationHandler(
        ICurrentContext currentContext,
        IFeatureService featureService)
    {
        _currentContext = currentContext;
        _featureService = featureService;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        CollectionOperationRequirement requirement)
    {
        // Acting user is not authenticated, fail
        if (!_currentContext.UserId.HasValue)
        {
            context.Fail();
            return;
        }

        if (requirement.OrganizationId == default)
        {
            context.Fail();
            return;
        }

        var org = _currentContext.GetOrganization(requirement.OrganizationId);

        switch (requirement)
        {
            case not null when requirement.Name == nameof(CollectionOperations.ReadAll):
                await CanReadAllAsync(context, requirement, org);
                break;

            case not null when requirement.Name == nameof(CollectionOperations.ReadAllWithAccess):
                await CanReadAllWithAccessAsync(context, requirement, org);
                break;
        }
    }

    private async Task CanReadAllAsync(AuthorizationHandlerContext context, CollectionOperationRequirement requirement,
        CurrentContextOrganization? org)
    {
        // Owners, Admins, and users with EditAnyCollection, DeleteAnyCollection,
        // or AccessImportExport permission can always read a collection
        if (org is
        { Type: OrganizationUserType.Owner or OrganizationUserType.Admin } or
        { Permissions.EditAnyCollection: true } or
        { Permissions.DeleteAnyCollection: true } or
        { Permissions.AccessImportExport: true } or
        { Permissions.ManageGroups: true })
        {
            context.Succeed(requirement);
            return;
        }

        // Allow provider users to read collections if they are a provider for the target organization
        if (await _currentContext.ProviderUserForOrgAsync(requirement.OrganizationId))
        {
            context.Succeed(requirement);
        }
    }

    private async Task CanReadAllWithAccessAsync(AuthorizationHandlerContext context, CollectionOperationRequirement requirement,
        CurrentContextOrganization? org)
    {
        // Owners, Admins, and users with EditAnyCollection or DeleteAnyCollection
        // permission can always read a collection
        if (org is
        { Type: OrganizationUserType.Owner or OrganizationUserType.Admin } or
        { Permissions.EditAnyCollection: true } or
        { Permissions.DeleteAnyCollection: true } or
        { Permissions.ManageUsers: true } or
        { Permissions.ManageGroups: true })
        {
            context.Succeed(requirement);
            return;
        }

        // Allow provider users to read collections if they are a provider for the target organization
        if (await _currentContext.ProviderUserForOrgAsync(requirement.OrganizationId))
        {
            context.Succeed(requirement);
        }
    }
}
