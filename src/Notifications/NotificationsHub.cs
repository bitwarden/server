using Bit.Core.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Bit.Notifications;

[Authorize("Application")]
public class NotificationsHub : Microsoft.AspNetCore.SignalR.Hub
{
    private readonly IOptions<NotificationsSettings> _settings;

    public NotificationsHub(IOptions<NotificationsSettings> settings)
    {
        _settings = settings;
    }

    public override async Task OnConnectedAsync()
    {
        foreach (var group in GetGroups(HubConnectionClaims.From(Context.User)))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, group);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        foreach (var group in GetGroups(HubConnectionClaims.From(Context.User)))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, group);
        }

        await base.OnDisconnectedAsync(exception);
    }

    private IEnumerable<string> GetGroups(HubConnectionClaims claims)
    {
        var clientType = claims.ClientType;
        if (clientType != ClientType.All && claims.UserId.HasValue)
        {
            yield return GetUserGroup(claims.UserId.Value, clientType);
        }

        var installationId = _settings.Value.InstallationId;
        if (installationId != Guid.Empty)
        {
            yield return GetInstallationGroup(installationId);
            if (clientType != ClientType.All)
            {
                yield return GetInstallationGroup(installationId, clientType);
            }
        }

        foreach (var organizationId in claims.OrganizationIds)
        {
            yield return GetOrganizationGroup(organizationId);
            if (clientType != ClientType.All)
            {
                yield return GetOrganizationGroup(organizationId, clientType);
            }
        }
    }

    public static string GetInstallationGroup(Guid installationId, ClientType? clientType = null)
    {
        return clientType is null or ClientType.All
            ? $"Installation_{installationId}"
            : $"Installation_ClientType_{installationId}_{clientType}";
    }

    public static string GetUserGroup(Guid userId, ClientType clientType)
    {
        return $"UserClientType_{userId}_{clientType}";
    }

    public static string GetOrganizationGroup(Guid organizationId, ClientType? clientType = null)
    {
        return clientType is null or ClientType.All
            ? $"Organization_{organizationId}"
            : $"OrganizationClientType_{organizationId}_{clientType}";
    }
}
