using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.SecretsManager.AuthorizationRequirements;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Queries.Interfaces;
using Bit.Core.SecretsManager.Repositories;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Commercial.Core.SecretsManager.AuthorizationHandlers.Secrets;

public class BulkSecretAuthorizationHandler : AuthorizationHandler<BulkSecretOperationRequirement, IReadOnlyList<Secret>>
{
    private readonly ICurrentContext _currentContext;
    private readonly IAccessClientQuery _accessClientQuery;
    private readonly ISecretRepository _secretRepository;

    public BulkSecretAuthorizationHandler(
        ICurrentContext currentContext,
        IAccessClientQuery accessClientQuery,
        ISecretRepository secretRepository)
    {
        _currentContext = currentContext;
        _accessClientQuery = accessClientQuery;
        _secretRepository = secretRepository;
    }

    protected override async Task HandleRequirementAsync(
      AuthorizationHandlerContext context,
      BulkSecretOperationRequirement requirement,
      IReadOnlyList<Secret> resource)
    {
        var secretsByOrganizationId = resource.GroupBy(s => s.OrganizationId).ToArray();

        // All the secrets should be part of a single organization
        if (secretsByOrganizationId.Length != 1)
        {
            return;
        }

        var organizationId = secretsByOrganizationId[0].Key;

        if (!_currentContext.AccessSecretsManager(organizationId))
        {
            return;
        }

        var (accessClient, userId) = await _accessClientQuery.GetAccessClientAsync(context.User, organizationId);

        if (requirement == BulkSecretOperations.Update)
        {
            if (!await CanBulkUpdateSecretsAsync(resource, accessClient, userId))
            {
                return;
            }

            context.Succeed(requirement);
        }
    }

    private async Task<bool> CanBulkUpdateSecretsAsync(
        IReadOnlyList<Secret> secrets,
        AccessClientType accessClientType,
        Guid userId)
    {
        var secretAccesses = await _secretRepository.AccessToSecretsAsync(
          secrets.Select(s => s.Id).ToArray(), userId, accessClientType);

        // If we don't have the write permission
        return secretAccesses.All(a => a.Value.Write);
    }
}
