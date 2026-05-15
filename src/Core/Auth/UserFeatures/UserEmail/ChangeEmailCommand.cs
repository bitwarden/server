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
        IOrganizationDomainRepository organizationDomainRepository,
        IPushNotificationService pushService,
        IStripeSyncService stripeSyncService,
        IOrganizationRepository organizationRepository) : IChangeEmailCommand
{
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IOrganizationDomainRepository _organizationDomainRepository = organizationDomainRepository;
    private readonly IPushNotificationService _pushService = pushService;
    private readonly IStripeSyncService _stripeSyncService = stripeSyncService;
    private readonly IOrganizationRepository _organizationRepository = organizationRepository;

    public async Task ChangeEmailAsync(User user, string newEmail)
    {
        // Will throw if the new email violates a claiming organization's domain policy.
        await EnsureNewEmailMatchesClaimedDomainAsync(user, newEmail);

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

    /// <summary>
    /// Verifies that <paramref name="newEmail"/>'s domain is permissible given any organizations
    /// that claim the user. If the user is claimed, the new email's domain must match one of the
    /// claiming organization's verified domains; otherwise throws <see cref="BadRequestException"/>.
    /// </summary>
    private async Task EnsureNewEmailMatchesClaimedDomainAsync(User user, string newEmail)
    {
        var organizationsWithVerifiedUserEmailDomain =
            await _organizationRepository.GetByVerifiedUserEmailDomainAsync(user.Id);

        // Organizations must be enabled and able to have verified domains.
        var claimingOrganizations = organizationsWithVerifiedUserEmailDomain
            .Where(organization => organization is { Enabled: true, UseOrganizationDomains: true })
            .ToList();

        if (claimingOrganizations.Count == 0)
        {
            return;
        }

        var newDomain = CoreHelpers.GetEmailDomain(newEmail);
        var verifiedDomains = await _organizationDomainRepository.GetVerifiedDomainsByOrganizationIdsAsync(
            claimingOrganizations.Select(org => org.Id));

        if (verifiedDomains.Any(verifiedDomain => verifiedDomain.DomainName == newDomain))
        {
            return;
        }

        throw new BadRequestException("Your new email must match your organization domain.");
    }
}
