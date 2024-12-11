using System.Security.Claims;
using Bit.Core.Enums;

namespace Bit.Core.SecretsManager.Queries.Interfaces;

public interface IAccessClientQuery
{
    Task<(AccessClientType AccessClientType, Guid UserId)> GetAccessClientAsync(
        ClaimsPrincipal claimsPrincipal,
        Guid organizationId
    );
}
