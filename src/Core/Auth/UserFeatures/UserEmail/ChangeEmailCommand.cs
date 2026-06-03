using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationDomains.Interfaces;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Auth.UserFeatures.UserEmail;

public class ChangeEmailCommand(
        IUserRepository userRepository,
        IStripeSyncService stripeSyncService,
        IOrganizationDomainAllowEmailChangeQuery organizationDomainAllowEmailChangeQuery,
        TimeProvider timeProvider,
        ILogger<ChangeEmailCommand> logger) : IChangeEmailCommand
{
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IStripeSyncService _stripeSyncService = stripeSyncService;
    private readonly IOrganizationDomainAllowEmailChangeQuery _organizationDomainAllowEmailChangeQuery = organizationDomainAllowEmailChangeQuery;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly ILogger<ChangeEmailCommand> _logger = logger;

    /// <inheritdoc />
    public async Task ChangeEmailAsync(User user, string newEmail)
    {
        await _organizationDomainAllowEmailChangeQuery.IsAllowedAsync(user, newEmail);

        var existingUser = await _userRepository.GetByEmailAsync(newEmail);
        if (existingUser != null && existingUser.Id != user.Id)
        {
            throw new BadRequestException("Email already in use.");
        }

        var previousEmail = user.Email;
        var previousRevisionDate = user.RevisionDate;
        var previousAccountRevisionDate = user.AccountRevisionDate;
        var previousLastEmailChangeDate = user.LastEmailChangeDate;

        var now = _timeProvider.GetUtcNow().UtcDateTime;
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
            catch (Exception stripeEx)
            {
                _logger.LogWarning(stripeEx, "Failed to sync email change to Stripe for user {UserId}. Reverting email change.", user.Id);

                user.Email = previousEmail;
                user.RevisionDate = previousRevisionDate;
                user.AccountRevisionDate = previousAccountRevisionDate;
                user.LastEmailChangeDate = previousLastEmailChangeDate;

                try
                {
                    await _userRepository.ReplaceAsync(user);
                }
                catch (Exception rollbackEx)
                {
                    // Log a higher level since user may be in an inconsistent state and may require manual intervention.
                    _logger.LogError(
                        rollbackEx,
                        "Rollback of email change failed for user {UserId} after Stripe sync error; user record may be in an inconsistent state.",
                        user.Id);
                }
                throw;
            }
        }
    }
}
