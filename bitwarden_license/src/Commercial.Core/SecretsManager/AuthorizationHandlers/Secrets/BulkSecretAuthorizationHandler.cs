#nullable enable
using Bit.Core.Context;
using Bit.Core.SecretsManager.AuthorizationRequirements;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Queries.Interfaces;
using Bit.Core.SecretsManager.Repositories;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Commercial.Core.SecretsManager.AuthorizationHandlers.Secrets;

public class
    BulkSecretAuthorizationHandler : AuthorizationHandler<BulkSecretOperationRequirement, IReadOnlyList<Secret>>
{
    private readonly IAccessClientQuery _accessClientQuery;
    private readonly ICurrentContext _currentContext;
    private readonly ISecretRepository _secretRepository;

    public BulkSecretAuthorizationHandler(ICurrentContext currentContext, IAccessClientQuery accessClientQuery,
        ISecretRepository secretRepository)
    {
        _currentContext = currentContext;
        _accessClientQuery = accessClientQuery;
        _secretRepository = secretRepository;
    }


    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        BulkSecretOperationRequirement requirement,
        IReadOnlyList<Secret> resources)
    {
        // Ensure all secrets belong to the same organization.
        var organizationId = resources[0].OrganizationId;
        if (resources.Any(secret => secret.OrganizationId != organizationId) ||
            !_currentContext.AccessSecretsManager(organizationId))
        {
            return;
        }

        switch (requirement)
        {
            case not null when requirement == BulkSecretOperations.ReadAll:
                await CanReadAllAsync(context, requirement, resources, organizationId);
                break;
            default:
                throw new ArgumentException("Unsupported operation requirement type provided.", nameof(requirement));
        }
    }

    private async Task CanReadAllAsync(AuthorizationHandlerContext context,
        BulkSecretOperationRequirement requirement, IReadOnlyList<Secret> resources, Guid organizationId)
    {
        var (accessClient, userId) = await _accessClientQuery.GetAccessClientAsync(context.User, organizationId);

        var secretsAccess =
            await _secretRepository.AccessToSecretsAsync(resources.Select(s => s.Id), userId, accessClient);

        if (secretsAccess.Count == resources.Count &&
            secretsAccess.All(a => a.Value.Read))
        {
            context.Succeed(requirement);
        }
    }
}
