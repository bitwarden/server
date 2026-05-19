using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;

namespace Bit.Core.Auth.UserFeatures.UserEmail;

public class ChangeEmailCommand(
        IUserRepository userRepository,
        IPushNotificationService pushService,
        IStripeSyncService stripeSyncService) : IChangeEmailCommand
{
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IPushNotificationService _pushService = pushService;
    private readonly IStripeSyncService _stripeSyncService = stripeSyncService;

    /// <inheritdoc />
    public async Task ChangeEmailAsync(User user, string newEmail)
    {
        var existingUser = await _userRepository.GetByEmailAsync(newEmail);
        if (existingUser != null && existingUser.Id != user.Id)
        {
            throw new BadRequestException("Email already in use.");
        }

        var previousEmail = user.Email;
        var previousRevisionDate = user.RevisionDate;
        var previousAccountRevisionDate = user.AccountRevisionDate;
        var previousLastEmailChangeDate = user.LastEmailChangeDate;

        var now = DateTime.UtcNow;
        user.Email = newEmail;
        user.EmailVerified = true;
        user.LastEmailChangeDate = now;
        user.RevisionDate = user.AccountRevisionDate = now;

        await _userRepository.ReplaceAsync(user);

        if (user.Gateway == GatewayType.Stripe)
        {
            try
            {
                var billingEmail = user.BillingEmailAddress();
                if (user.GatewayCustomerId != null && billingEmail != null)
                {
                    await _stripeSyncService.UpdateCustomerEmailAddressAsync(user.GatewayCustomerId, billingEmail);
                }
            }
            catch
            {
                user.Email = previousEmail;
                user.RevisionDate = previousRevisionDate;
                user.AccountRevisionDate = previousAccountRevisionDate;
                user.LastEmailChangeDate = previousLastEmailChangeDate;
                await _userRepository.ReplaceAsync(user);
                throw;
            }
        }

        if (user.HasMasterPassword())
        {
            await _pushService.PushLogOutAsync(user.Id);
        }
        else
        {
            await _pushService.PushSyncSettingsAsync(user.Id);
        }
    }
}
