using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Entities.Provider;
using Bit.Core.Enums;
using Bit.Core.Enums.Provider;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.Business.Provider;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.DataProtection;

namespace Bit.Commercial.Core.Services;

public class ProviderService : IProviderService
{
    public static PlanType[] ProviderDisllowedOrganizationTypes = new[] { PlanType.Free, PlanType.FamiliesAnnually, PlanType.FamiliesAnnually2019 };

    private readonly IDataProtector _dataProtector;
    private readonly IMailService _mailService;
    private readonly IEventService _eventService;
    private readonly GlobalSettings _globalSettings;
    private readonly IProviderRepository _providerRepository;
    private readonly IProviderUserRepository _providerUserRepository;
    private readonly IProviderOrganizationRepository _providerOrganizationRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUserService _userService;
    private readonly IOrganizationService _organizationService;
    private readonly ICurrentContext _currentContext;

    public ProviderService(IProviderRepository providerRepository, IProviderUserRepository providerUserRepository,
        IProviderOrganizationRepository providerOrganizationRepository, IUserRepository userRepository,
        IUserService userService, IOrganizationService organizationService, IMailService mailService,
        IDataProtectionProvider dataProtectionProvider, IEventService eventService,
        IOrganizationRepository organizationRepository, GlobalSettings globalSettings,
        ICurrentContext currentContext)
    {
        _providerRepository = providerRepository;
        _providerUserRepository = providerUserRepository;
        _providerOrganizationRepository = providerOrganizationRepository;
        _organizationRepository = organizationRepository;
        _userRepository = userRepository;
        _userService = userService;
        _organizationService = organizationService;
        _mailService = mailService;
        _eventService = eventService;
        _globalSettings = globalSettings;
        _dataProtector = dataProtectionProvider.CreateProtector("ProviderServiceDataProtector");
        _currentContext = currentContext;
    }

    public async Task CreateAsync(string ownerEmail)
    {
        var owner = await _userRepository.GetByEmailAsync(ownerEmail);
        if (owner == null)
        {
            throw new BadRequestException("Invalid owner. Owner must be an existing Bitwarden user.");
        }

        var provider = new Provider
        {
            Status = ProviderStatusType.Pending,
            Enabled = true,
            UseEvents = true,
        };
        await _providerRepository.CreateAsync(provider);

        var providerUser = new ProviderUser
        {
            ProviderId = provider.Id,
            UserId = owner.Id,
            Type = ProviderUserType.ProviderAdmin,
            Status = ProviderUserStatusType.Confirmed,
        };
        await _providerUserRepository.CreateAsync(providerUser);
        await SendProviderSetupInviteEmailAsync(provider, owner.Email);
    }

    public async Task<Provider> CompleteSetupAsync(Provider provider, Guid ownerUserId, string token, string key)
    {
        var owner = await _userService.GetUserByIdAsync(ownerUserId);
        if (owner == null)
        {
            throw new BadRequestException("Invalid owner.");
        }

        if (provider.Status != ProviderStatusType.Pending)
        {
            throw new BadRequestException("Provider is already setup.");
        }

        if (!CoreHelpers.TokenIsValid("ProviderSetupInvite", _dataProtector, token, owner.Email, provider.Id,
            _globalSettings.OrganizationInviteExpirationHours))
        {
            throw new BadRequestException("Invalid token.");
        }

        var providerUser = await _providerUserRepository.GetByProviderUserAsync(provider.Id, ownerUserId);
        if (!(providerUser is { Type: ProviderUserType.ProviderAdmin }))
        {
            throw new BadRequestException("Invalid owner.");
        }

        provider.Status = ProviderStatusType.Created;
        await _providerRepository.UpsertAsync(provider);

        providerUser.Key = key;
        await _providerUserRepository.ReplaceAsync(providerUser);

        return provider;
    }

    public async Task UpdateAsync(Provider provider, bool updateBilling = false)
    {
        if (provider.Id == default)
        {
            throw new ArgumentException("Cannot create provider this way.");
        }

        await _providerRepository.ReplaceAsync(provider);
    }

    public async Task<List<ProviderUser>> InviteUserAsync(ProviderUserInvite<string> invite)
    {
        if (!_currentContext.ProviderManageUsers(invite.ProviderId))
        {
            throw new InvalidOperationException("Invalid permissions.");
        }

        var emails = invite?.UserIdentifiers;
        var invitingUser = await _providerUserRepository.GetByProviderUserAsync(invite.ProviderId, invite.InvitingUserId);

        var provider = await _providerRepository.GetByIdAsync(invite.ProviderId);
        if (provider == null || emails == null || !emails.Any())
        {
            throw new NotFoundException();
        }

        var providerUsers = new List<ProviderUser>();
        foreach (var email in emails)
        {
            // Make sure user is not already invited
            var existingProviderUserCount =
                await _providerUserRepository.GetCountByProviderAsync(invite.ProviderId, email, false);
            if (existingProviderUserCount > 0)
            {
                continue;
            }

            var providerUser = new ProviderUser
            {
                ProviderId = invite.ProviderId,
                UserId = null,
                Email = email.ToLowerInvariant(),
                Key = null,
                Type = invite.Type,
                Status = ProviderUserStatusType.Invited,
                CreationDate = DateTime.UtcNow,
                RevisionDate = DateTime.UtcNow,
            };

            await _providerUserRepository.CreateAsync(providerUser);

            await SendInviteAsync(providerUser, provider);
            providerUsers.Add(providerUser);
        }

        await _eventService.LogProviderUsersEventAsync(providerUsers.Select(pu => (pu, EventType.ProviderUser_Invited, null as DateTime?)));

        return providerUsers;
    }

    public async Task<List<Tuple<ProviderUser, string>>> ResendInvitesAsync(ProviderUserInvite<Guid> invite)
    {
        if (!_currentContext.ProviderManageUsers(invite.ProviderId))
        {
            throw new BadRequestException("Invalid permissions.");
        }

        var providerUsers = await _providerUserRepository.GetManyAsync(invite.UserIdentifiers);
        var provider = await _providerRepository.GetByIdAsync(invite.ProviderId);

        var result = new List<Tuple<ProviderUser, string>>();
        foreach (var providerUser in providerUsers)
        {
            if (providerUser.Status != ProviderUserStatusType.Invited || providerUser.ProviderId != invite.ProviderId)
            {
                result.Add(Tuple.Create(providerUser, "User invalid."));
                continue;
            }

            await SendInviteAsync(providerUser, provider);
            result.Add(Tuple.Create(providerUser, ""));
        }

        return result;
    }

    public async Task<ProviderUser> AcceptUserAsync(Guid providerUserId, User user, string token)
    {
        var providerUser = await _providerUserRepository.GetByIdAsync(providerUserId);
        if (providerUser == null)
        {
            throw new BadRequestException("User invalid.");
        }

        if (providerUser.Status != ProviderUserStatusType.Invited)
        {
            throw new BadRequestException("Already accepted.");
        }

        if (!CoreHelpers.TokenIsValid("ProviderUserInvite", _dataProtector, token, user.Email, providerUser.Id,
            _globalSettings.OrganizationInviteExpirationHours))
        {
            throw new BadRequestException("Invalid token.");
        }

        if (string.IsNullOrWhiteSpace(providerUser.Email) ||
            !providerUser.Email.Equals(user.Email, StringComparison.InvariantCultureIgnoreCase))
        {
            throw new BadRequestException("User email does not match invite.");
        }

        providerUser.Status = ProviderUserStatusType.Accepted;
        providerUser.UserId = user.Id;
        providerUser.Email = null;

        await _providerUserRepository.ReplaceAsync(providerUser);

        return providerUser;
    }

    public async Task<List<Tuple<ProviderUser, string>>> ConfirmUsersAsync(Guid providerId, Dictionary<Guid, string> keys,
        Guid confirmingUserId)
    {
        var providerUsers = await _providerUserRepository.GetManyAsync(keys.Keys);
        var validProviderUsers = providerUsers
            .Where(u => u.UserId != null)
            .ToList();

        if (!validProviderUsers.Any())
        {
            return new List<Tuple<ProviderUser, string>>();
        }

        var validOrganizationUserIds = validProviderUsers.Select(u => u.UserId.Value).ToList();

        var provider = await _providerRepository.GetByIdAsync(providerId);
        var users = await _userRepository.GetManyAsync(validOrganizationUserIds);

        var keyedFilteredUsers = validProviderUsers.ToDictionary(u => u.UserId.Value, u => u);

        var result = new List<Tuple<ProviderUser, string>>();
        var events = new List<(ProviderUser, EventType, DateTime?)>();

        foreach (var user in users)
        {
            if (!keyedFilteredUsers.ContainsKey(user.Id))
            {
                continue;
            }
            var providerUser = keyedFilteredUsers[user.Id];
            try
            {
                if (providerUser.Status != ProviderUserStatusType.Accepted || providerUser.ProviderId != providerId)
                {
                    throw new BadRequestException("Invalid user.");
                }

                providerUser.Status = ProviderUserStatusType.Confirmed;
                providerUser.Key = keys[providerUser.Id];
                providerUser.Email = null;

                await _providerUserRepository.ReplaceAsync(providerUser);
                events.Add((providerUser, EventType.ProviderUser_Confirmed, null));
                await _mailService.SendProviderConfirmedEmailAsync(provider.Name, user.Email);
                result.Add(Tuple.Create(providerUser, ""));
            }
            catch (BadRequestException e)
            {
                result.Add(Tuple.Create(providerUser, e.Message));
            }
        }

        await _eventService.LogProviderUsersEventAsync(events);

        return result;
    }

    public async Task SaveUserAsync(ProviderUser user, Guid savingUserId)
    {
        if (user.Id.Equals(default))
        {
            throw new BadRequestException("Invite the user first.");
        }

        if (user.Type != ProviderUserType.ProviderAdmin &&
            !await HasConfirmedProviderAdminExceptAsync(user.ProviderId, new[] { user.Id }))
        {
            throw new BadRequestException("Provider must have at least one confirmed ProviderAdmin.");
        }

        await _providerUserRepository.ReplaceAsync(user);
        await _eventService.LogProviderUserEventAsync(user, EventType.ProviderUser_Updated);
    }

    public async Task<List<Tuple<ProviderUser, string>>> DeleteUsersAsync(Guid providerId,
        IEnumerable<Guid> providerUserIds, Guid deletingUserId)
    {
        var provider = await _providerRepository.GetByIdAsync(providerId);

        if (provider == null)
        {
            throw new NotFoundException();
        }

        var providerUsers = await _providerUserRepository.GetManyAsync(providerUserIds);
        var users = await _userRepository.GetManyAsync(providerUsers.Where(pu => pu.UserId.HasValue)
            .Select(pu => pu.UserId.Value));
        var keyedUsers = users.ToDictionary(u => u.Id);

        if (!await HasConfirmedProviderAdminExceptAsync(providerId, providerUserIds))
        {
            throw new BadRequestException("Provider must have at least one confirmed ProviderAdmin.");
        }

        var result = new List<Tuple<ProviderUser, string>>();
        var deletedUserIds = new List<Guid>();
        var events = new List<(ProviderUser, EventType, DateTime?)>();

        foreach (var providerUser in providerUsers)
        {
            try
            {
                if (providerUser.ProviderId != providerId)
                {
                    throw new BadRequestException("Invalid user.");
                }
                if (providerUser.UserId == deletingUserId)
                {
                    throw new BadRequestException("You cannot remove yourself.");
                }

                events.Add((providerUser, EventType.ProviderUser_Removed, null));

                var user = keyedUsers.GetValueOrDefault(providerUser.UserId.GetValueOrDefault());
                var email = user == null ? providerUser.Email : user.Email;
                if (!string.IsNullOrWhiteSpace(email))
                {
                    await _mailService.SendProviderUserRemoved(provider.Name, email);
                }

                result.Add(Tuple.Create(providerUser, ""));
                deletedUserIds.Add(providerUser.Id);
            }
            catch (BadRequestException e)
            {
                result.Add(Tuple.Create(providerUser, e.Message));
            }

            await _providerUserRepository.DeleteManyAsync(deletedUserIds);
        }

        await _eventService.LogProviderUsersEventAsync(events);

        return result;
    }

    public async Task AddOrganization(Guid providerId, Guid organizationId, Guid addingUserId, string key)
    {
        var po = await _providerOrganizationRepository.GetByOrganizationId(organizationId);
        if (po != null)
        {
            throw new BadRequestException("Organization already belongs to a provider.");
        }

        var organization = await _organizationRepository.GetByIdAsync(organizationId);
        ThrowOnInvalidPlanType(organization.PlanType);

        var providerOrganization = new ProviderOrganization
        {
            ProviderId = providerId,
            OrganizationId = organizationId,
            Key = key,
        };

        await _providerOrganizationRepository.CreateAsync(providerOrganization);
        await _eventService.LogProviderOrganizationEventAsync(providerOrganization, EventType.ProviderOrganization_Added);
    }

    public async Task<ProviderOrganization> CreateOrganizationAsync(Guid providerId,
        OrganizationSignup organizationSignup, string clientOwnerEmail, User user)
    {
        ThrowOnInvalidPlanType(organizationSignup.Plan);

        var (organization, _) = await _organizationService.SignUpAsync(organizationSignup, true);

        var providerOrganization = new ProviderOrganization
        {
            ProviderId = providerId,
            OrganizationId = organization.Id,
            Key = organizationSignup.OwnerKey,
        };

        await _providerOrganizationRepository.CreateAsync(providerOrganization);
        await _eventService.LogProviderOrganizationEventAsync(providerOrganization, EventType.ProviderOrganization_Created);

        await _organizationService.InviteUsersAsync(organization.Id, user.Id,
            new (OrganizationUserInvite, string)[]
            {
                (
                    new OrganizationUserInvite
                    {
                        Emails = new[] { clientOwnerEmail },
                        AccessAll = true,
                        Type = OrganizationUserType.Owner,
                        Permissions = null,
                        Collections = Array.Empty<SelectionReadOnly>(),
                    },
                    null
                )
            });

        return providerOrganization;
    }

    public async Task RemoveOrganizationAsync(Guid providerId, Guid providerOrganizationId, Guid removingUserId)
    {
        var providerOrganization = await _providerOrganizationRepository.GetByIdAsync(providerOrganizationId);
        if (providerOrganization == null || providerOrganization.ProviderId != providerId)
        {
            throw new BadRequestException("Invalid organization.");
        }

        if (!await _organizationService.HasConfirmedOwnersExceptAsync(providerOrganization.OrganizationId, new Guid[] { }, includeProvider: false))
        {
            throw new BadRequestException("Organization needs to have at least one confirmed owner.");
        }

        await _providerOrganizationRepository.DeleteAsync(providerOrganization);
        await _eventService.LogProviderOrganizationEventAsync(providerOrganization, EventType.ProviderOrganization_Removed);
    }

    public async Task ResendProviderSetupInviteEmailAsync(Guid providerId, Guid ownerId)
    {
        var provider = await _providerRepository.GetByIdAsync(providerId);
        var owner = await _userRepository.GetByIdAsync(ownerId);
        if (owner == null)
        {
            throw new BadRequestException("Invalid owner.");
        }
        await SendProviderSetupInviteEmailAsync(provider, owner.Email);
    }

    private async Task SendProviderSetupInviteEmailAsync(Provider provider, string ownerEmail)
    {
        var token = _dataProtector.Protect($"ProviderSetupInvite {provider.Id} {ownerEmail} {CoreHelpers.ToEpocMilliseconds(DateTime.UtcNow)}");
        await _mailService.SendProviderSetupInviteEmailAsync(provider, token, ownerEmail);
    }

    public async Task LogProviderAccessToOrganizationAsync(Guid organizationId)
    {
        if (organizationId == default)
        {
            return;
        }

        var providerOrganization = await _providerOrganizationRepository.GetByOrganizationId(organizationId);
        var organization = await _organizationRepository.GetByIdAsync(organizationId);
        if (providerOrganization != null)
        {
            await _eventService.LogProviderOrganizationEventAsync(providerOrganization, EventType.ProviderOrganization_VaultAccessed);
        }
        if (organization != null)
        {
            await _eventService.LogOrganizationEventAsync(organization, EventType.Organization_VaultAccessed);
        }
    }

    private async Task SendInviteAsync(ProviderUser providerUser, Provider provider)
    {
        var nowMillis = CoreHelpers.ToEpocMilliseconds(DateTime.UtcNow);
        var token = _dataProtector.Protect(
            $"ProviderUserInvite {providerUser.Id} {providerUser.Email} {nowMillis}");
        await _mailService.SendProviderInviteEmailAsync(provider.Name, providerUser, token, providerUser.Email);
    }

    private async Task<bool> HasConfirmedProviderAdminExceptAsync(Guid providerId, IEnumerable<Guid> providerUserIds)
    {
        var providerAdmins = await _providerUserRepository.GetManyByProviderAsync(providerId,
            ProviderUserType.ProviderAdmin);
        var confirmedOwners = providerAdmins.Where(o => o.Status == ProviderUserStatusType.Confirmed);
        var confirmedOwnersIds = confirmedOwners.Select(u => u.Id);
        return confirmedOwnersIds.Except(providerUserIds).Any();
    }

    private void ThrowOnInvalidPlanType(PlanType requestedType)
    {
        if (ProviderDisllowedOrganizationTypes.Contains(requestedType))
        {
            throw new BadRequestException($"Providers cannot manage organizations with the requested plan type ({requestedType}). Only Teams and Enterprise accounts are allowed.");
        }
    }
}
