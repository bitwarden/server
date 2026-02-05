using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Enums;
using Bit.Core.Models;
using Bit.Core.Platform.Push;

namespace Bit.Billing.Services.Implementations;

public class PushNotificationAdapter(
    IProviderUserRepository providerUserRepository,
    IPushNotificationService pushNotificationService) : IPushNotificationAdapter
{
    public Task NotifyBankAccountVerifiedAsync(Organization organization) =>
        pushNotificationService.PushAsync(new PushNotification<OrganizationBankAccountVerifiedPushNotification>
        {
            Type = PushType.OrganizationBankAccountVerified,
            Target = NotificationTarget.Organization,
            TargetId = organization.Id,
            Payload = new OrganizationBankAccountVerifiedPushNotification
            {
                OrganizationId = organization.Id
            },
            ExcludeCurrentContext = false
        });

    public async Task NotifyBankAccountVerifiedAsync(Provider provider)
    {
        var providerUsers = await providerUserRepository.GetManyByProviderAsync(provider.Id);
        var providerAdmins = providerUsers.Where(providerUser => providerUser is
        {
            Type: ProviderUserType.ProviderAdmin,
            Status: ProviderUserStatusType.Confirmed,
            UserId: not null
        }).ToList();

        if (providerAdmins.Count > 0)
        {
            var tasks = providerAdmins.Select(providerAdmin => pushNotificationService.PushAsync(
                new PushNotification<ProviderBankAccountVerifiedPushNotification>
                {
                    Type = PushType.ProviderBankAccountVerified,
                    Target = NotificationTarget.User,
                    TargetId = providerAdmin.UserId!.Value,
                    Payload = new ProviderBankAccountVerifiedPushNotification
                    {
                        ProviderId = provider.Id,
                        AdminId = providerAdmin.UserId!.Value
                    },
                    ExcludeCurrentContext = false
                }));

            await Task.WhenAll(tasks);
        }
    }

    public Task NotifyEnabledChangedAsync(Organization organization) =>
        pushNotificationService.PushAsync(new PushNotification<OrganizationStatusPushNotification>
        {
            Type = PushType.SyncOrganizationStatusChanged,
            Target = NotificationTarget.Organization,
            TargetId = organization.Id,
            Payload = new OrganizationStatusPushNotification
            {
                OrganizationId = organization.Id,
                Enabled = organization.Enabled,
            },
            ExcludeCurrentContext = false,
        });
}
