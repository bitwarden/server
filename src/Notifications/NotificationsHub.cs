using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Notifications;

[Authorize("Application")]
public class NotificationsHub : Microsoft.AspNetCore.SignalR.Hub
{
    private readonly ConnectionCounter _connectionCounter;
    private readonly GlobalSettings _globalSettings;

    public NotificationsHub(ConnectionCounter connectionCounter, GlobalSettings globalSettings)
    {
        _connectionCounter = connectionCounter;
        _globalSettings = globalSettings;
    }

    public override async Task OnConnectedAsync()
    {
        var currentContext = new CurrentContext(null, null);
        await currentContext.BuildAsync(Context.User, _globalSettings);

        var clientType = DeviceTypes.ToClientType(currentContext.DeviceType);
        if (clientType != ClientType.All && currentContext.UserId.HasValue)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GetUserGroup(currentContext.UserId.Value, clientType));
        }

        if (currentContext.InstallationId.HasValue)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId,
                GetInstallationGroup(currentContext.InstallationId.Value));
            if (clientType != ClientType.All)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId,
                    GetInstallationGroup(currentContext.InstallationId.Value, clientType));
            }
        }

        if (currentContext.Organizations != null)
        {
            foreach (var org in currentContext.Organizations)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, GetOrganizationGroup(org.Id));
                if (clientType != ClientType.All)
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, GetOrganizationGroup(org.Id, clientType));
                }
            }
        }

        _connectionCounter.Increment();
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception exception)
    {
        var currentContext = new CurrentContext(null, null);
        await currentContext.BuildAsync(Context.User, _globalSettings);

        var clientType = DeviceTypes.ToClientType(currentContext.DeviceType);
        if (clientType != ClientType.All && currentContext.UserId.HasValue)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId,
                GetUserGroup(currentContext.UserId.Value, clientType));
        }

        if (currentContext.InstallationId.HasValue)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId,
                GetInstallationGroup(currentContext.InstallationId.Value));
            if (clientType != ClientType.All)
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId,
                    GetInstallationGroup(currentContext.InstallationId.Value, clientType));
            }
        }

        if (currentContext.Organizations != null)
        {
            foreach (var org in currentContext.Organizations)
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetOrganizationGroup(org.Id));
                if (clientType != ClientType.All)
                {
                    await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetOrganizationGroup(org.Id, clientType));
                }
            }
        }

        _connectionCounter.Decrement();
        await base.OnDisconnectedAsync(exception);
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
