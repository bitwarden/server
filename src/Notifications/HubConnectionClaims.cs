using System.Security.Claims;
using Bit.Core.Enums;
using Bit.Core.Utilities;

namespace Bit.Notifications;

/// <summary>
/// The parts of an access token that decide which SignalR groups a connection joins.
/// </summary>
internal sealed record HubConnectionClaims(Guid? UserId, ClientType ClientType, IReadOnlyList<Guid> OrganizationIds)
{
    private static readonly HubConnectionClaims _empty = new(null, ClientType.All, []);

    private static readonly string[] _organizationMembershipClaims = ["orgowner", "orgadmin", "orguser", "orgcustom"];

    public static HubConnectionClaims From(ClaimsPrincipal? user)
    {
        if (user is null || !user.Claims.Any())
        {
            return _empty;
        }

        // Send access tokens are not tied to a user or organization.
        if (user.FindFirst("type")?.Value == "Send")
        {
            return _empty;
        }

        Guid? userId = Guid.TryParse(user.FindFirst("sub")?.Value, out var subject) ? subject : null;

        DeviceType? deviceType = Enum.TryParse(user.FindFirst("devicetype")?.Value, out DeviceType parsed)
            ? parsed
            : null;

        var organizationIds = new List<Guid>();
        foreach (var claim in user.Claims.Where(c => _organizationMembershipClaims.Contains(c.Type)))
        {
            if (Guid.TryParse(claim.Value, out var organizationId))
            {
                organizationIds.Add(organizationId);
            }
        }

        // An organization API key authenticates as the organization itself, which then counts as a member.
        if (user.FindFirst("client_id")?.Value?.StartsWith("organization.") == true
            && Guid.TryParse(user.FindFirst("client_sub")?.Value, out var apiKeyOrganizationId))
        {
            organizationIds.Add(apiKeyOrganizationId);
        }

        return new HubConnectionClaims(userId, DeviceTypes.ToClientType(deviceType), organizationIds.Distinct().ToList());
    }
}
