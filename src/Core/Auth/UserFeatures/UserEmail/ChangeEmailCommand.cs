using Bit.Core.Auth.UserFeatures.UserEmail.Interfaces;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;

namespace Bit.Core.Auth.UserFeatures.UserEmail;

public class ChangeEmailCommand : IChangeEmailCommand
{
    private readonly IUserRepository _userRepository;
    private readonly IStripeSyncService _stripeSyncService;
    private readonly IPushNotificationService _pushNotificationService;

    public ChangeEmailCommand(
        IUserRepository userRepository,
        IStripeSyncService stripeSyncService,
        IPushNotificationService pushNotificationService)
    {
        _userRepository = userRepository;
        _stripeSyncService = stripeSyncService;
        _pushNotificationService = pushNotificationService;
    }

    public async Task ChangeEmailAsync(User user, string newEmail)
    {
        // ValidateClaimedUserDomainAsync.

        // Querying by email exposes a limited account-enumeration vector: a distinct error response
        // ("Email already in use.") vs. success lets a caller infer whether a Bitwarden account exists
        // at a given address. Callers are responsible for enforcing access controls before reaching this
        // point that bound who can probe and which addresses are reachable.
        var existingUser = await _userRepository.GetByEmailAsync(newEmail);
        if (existingUser != null && existingUser.Id != user.Id)
        {
            throw new BadRequestException("Email already in use.");
        }

        var previousEmail = user.Email;
        var now = DateTime.UtcNow;
        user.Email = newEmail;
        user.EmailVerified = true;
        user.RevisionDate = user.AccountRevisionDate = now;
        user.LastEmailChangeDate = now;
        await _userRepository.ReplaceAsync(user);

        if (user.Gateway == GatewayType.Stripe && user.GatewayCustomerId != null)
        {
            try
            {
                await _stripeSyncService.UpdateCustomerEmailAddressAsync(
                    user.GatewayCustomerId,
                    user.BillingEmailAddress()!);
            }
            catch
            {
                user.Email = previousEmail;
                user.RevisionDate = user.AccountRevisionDate = DateTime.UtcNow;
                await _userRepository.ReplaceAsync(user);
                throw;
            }
        }

        if (user.HasMasterPassword())
        {
            await _pushNotificationService.PushLogOutAsync(user.Id);
        }
        else
        {
            // Perform sync. Does revision date push a sync?
            // Make sure email is sent back on sync
        }
    }
}
