using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Utilities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;

public class ChangeEmailForPasswordlessUserCommand : IChangeEmailForPasswordlessUserCommand
{
    private readonly IUserRepository _userRepository;
    private readonly IOrganizationDomainRepository _organizationDomainRepository;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly IStripeSyncService _stripeSyncService;

    public ChangeEmailForPasswordlessUserCommand(
        IUserRepository userRepository,
        IOrganizationDomainRepository organizationDomainRepository,
        IPushNotificationService pushNotificationService,
        IStripeSyncService stripeSyncService)
    {
        _userRepository = userRepository;
        _organizationDomainRepository = organizationDomainRepository;
        _pushNotificationService = pushNotificationService;
        _stripeSyncService = stripeSyncService;
    }

    public async Task ChangeEmailAsync(Guid organizationId, OrganizationUser organizationUser, string newEmail)
    {
        var user = await _userRepository.GetByIdAsync(organizationUser.UserId!.Value)
            ?? throw new NotFoundException();

        if (user.HasMasterPassword())
        {
            throw new BadRequestException("User has a master password.");
        }

        var newDomain = CoreHelpers.GetEmailDomain(newEmail);
        var claimedDomain = await _organizationDomainRepository
            .GetDomainByOrgIdAndDomainNameAsync(organizationId, newDomain!);

        if (claimedDomain?.VerifiedDate == null)
        {
            throw new BadRequestException("The email domain is not claimed by the organization.");
        }

        // Querying by email exposes a limited account-enumeration vector: a distinct error response
        // ("Email already in use.") vs. success lets a ManageUsers admin infer whether a Bitwarden
        // account exists at a given address — even for non-members of this org. The risk is bounded by
        // two constraints already enforced above: (1) the caller must hold ManageUsers on the org, and
        // (2) newEmail must pass the org domain claim check, so only addresses within the org's own
        // verified domain can be probed. Probing is also destructive: a "miss" (no existing account)
        // succeeds and changes the member's email, making silent enumeration impractical.
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

        await _pushNotificationService.PushLogOutAsync(user.Id);
    }
}
