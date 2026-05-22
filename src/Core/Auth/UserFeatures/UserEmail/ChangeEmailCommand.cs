using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationDomains.Interfaces;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Utilities;

namespace Bit.Core.Auth.UserFeatures.UserEmail;

public class ChangeEmailCommand(
        IUserRepository userRepository,
        IPushNotificationService pushService,
        IStripeSyncService stripeSyncService,
        IOrganizationDomainAllowEmailChangeQuery organizationDomainAllowEmailChangeQuery,
        TimeProvider timeProvider) : IChangeEmailCommand
{
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IPushNotificationService _pushService = pushService;
    private readonly IStripeSyncService _stripeSyncService = stripeSyncService;
    private readonly IOrganizationDomainAllowEmailChangeQuery _organizationDomainAllowEmailChangeQuery = organizationDomainAllowEmailChangeQuery;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc />
    public async Task ChangeEmailAsync(User user, string newEmail)
    {
        await EnsureNewEmailDomainAllowedByPolicyAsync(user, newEmail);

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

        await _pushService.PushSyncSettingsAsync(user.Id);
    }

    /// <summary>
    /// Blocks an email change onto a domain claimed by an organization that has the
    /// BlockClaimedDomainAccountCreation policy enabled. Mirrors the gate enforced by
    /// RegisterUserCommand so the policy cannot be bypassed via email change.
    /// </summary>
    private async Task EnsureNewEmailDomainAllowedByPolicyAsync(User user, string newEmail)
    {
        // If the new email domain is the same as the current email domain, we can skip
        // the checks since it would be a noop in terms of policy and claiming organizations.
        // Null check for nullable reference types, but the email should always be valid at this point in the code.
        var newDomain = EmailValidation.GetDomain(newEmail);
        if (newDomain == EmailValidation.GetDomain(user.Email))
        {
            return;
        }

        var isAllowed = await _organizationDomainAllowEmailChangeQuery.IsAllowedAsync(user, newDomain);
        if (!isAllowed)
        {
            throw new BadRequestException("This email address is claimed by an organization using Bitwarden.");
        }
    }
}
