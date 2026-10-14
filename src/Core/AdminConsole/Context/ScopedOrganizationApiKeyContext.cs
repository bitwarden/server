using Microsoft.AspNetCore.Http;

namespace Bit.Core.AdminConsole.Context;

public class ScopedOrganizationApiKeyContext(IHttpContextAccessor httpContextAccessor) : IScopedOrganizationApiKeyContext
{
    public bool IsScopedOrganizationApiKey =>
        IsScopedOrganizationApiKeyClientId(httpContextAccessor.HttpContext?.User.FindFirst("client_id")?.Value);

    public static bool IsScopedOrganizationApiKeyClientId(string? clientId) =>
        clientId?.Split('.') is ["organization", var organizationIdPart, var keyIdPart] &&
        Guid.TryParse(organizationIdPart, out _) && Guid.TryParse(keyIdPart, out _);
}
