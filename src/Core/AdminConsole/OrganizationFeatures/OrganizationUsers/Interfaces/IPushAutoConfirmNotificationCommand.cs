namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;

public interface IPushAutoConfirmNotificationCommand
{
    Task PushAsync(Guid userId, Guid organizationId);
}
