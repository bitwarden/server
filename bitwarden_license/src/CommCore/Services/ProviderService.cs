using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.Enums;
using Bit.Core.Enums.Provider;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.Business.Provider;
using Bit.Core.Models.Table;
using Bit.Core.Models.Table.Provider;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.DataProtection;

namespace Bit.CommCore.Services
{
    public class ProviderService : IProviderService
    {
        private readonly IDataProtector _dataProtector;
        private readonly IMailService _mailService;
        private readonly IEventService _eventService;
        private readonly GlobalSettings _globalSettings;
        private readonly IProviderRepository _providerRepository;
        private readonly IProviderUserRepository _providerUserRepository;
        private readonly IProviderOrganizationRepository _providerOrganizationRepository;
        private readonly IUserRepository _userRepository;
        private readonly IUserService _userService;
        private readonly IOrganizationService _organizationService;

        public ProviderService(IProviderRepository providerRepository, IProviderUserRepository providerUserRepository,
            IProviderOrganizationRepository providerOrganizationRepository, IUserRepository userRepository,
            IUserService userService, IOrganizationService organizationService, IMailService mailService,
            IDataProtectionProvider dataProtectionProvider, IEventService eventService, GlobalSettings globalSettings)
        {
            _providerRepository = providerRepository;
            _providerUserRepository = providerUserRepository;
            _providerOrganizationRepository = providerOrganizationRepository;
            _userRepository = userRepository;
            _userService = userService;
            _organizationService = organizationService;
            _mailService = mailService;
            _eventService = eventService;
            _globalSettings = globalSettings;
            _dataProtector = dataProtectionProvider.CreateProtector("ProviderServiceDataProtector");
        }

        public async Task CreateAsync(string ownerEmail)
        {
            var owner = await _userRepository.GetByEmailAsync(ownerEmail);
            if (owner == null)
            {
                throw new BadRequestException("Invalid owner.");
            }

            var provider = new Provider
            {
                Status = ProviderStatusType.Pending,
                Enabled = true,
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

            var token = _dataProtector.Protect($"ProviderSetupInvite {provider.Id} {owner.Email} {CoreHelpers.ToEpocMilliseconds(DateTime.UtcNow)}");
            await _mailService.SendProviderSetupInviteEmailAsync(provider, token, owner.Email);
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

            if (!CoreHelpers.TokenIsValid("ProviderSetupInvite", _dataProtector, token, owner.Email, provider.Id, _globalSettings))
            {
                throw new BadRequestException("Invalid token.");
            }

            var providerUser = await _providerUserRepository.GetByProviderUserAsync(provider.Id, ownerUserId);
            if (!(providerUser is {Type: ProviderUserType.ProviderAdmin}))
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
                throw new ApplicationException("Cannot create provider this way.");
            }

            await _providerRepository.ReplaceAsync(provider);
        }

        public async Task<List<ProviderUser>> InviteUserAsync(Guid providerId, Guid invitingUserId,
            ProviderUserInvite invite)
        {
            var provider = await _providerRepository.GetByIdAsync(providerId);
            if (provider == null || invite?.Emails == null || !invite.Emails.Any())
            {
                throw new NotFoundException();
            }

            var providerUsers = new List<ProviderUser>();
            foreach (var email in invite.Emails)
            {
                // Make sure user is not already invited
                var existingProviderUserCount =
                    await _providerUserRepository.GetCountByProviderAsync(providerId, email, false);
                if (existingProviderUserCount > 0)
                {
                    continue;
                }

                var providerUser = new ProviderUser
                {
                    ProviderId = providerId,
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

        public async Task<List<Tuple<ProviderUser, string>>> ResendInvitesAsync(Guid providerId, Guid invitingUserId,
            IEnumerable<Guid> providerUsersId)
        {
            var providerUsers = await _providerUserRepository.GetManyAsync(providerUsersId);
            var provider = await _providerRepository.GetByIdAsync(providerId);

            var result = new List<Tuple<ProviderUser, string>>();
            foreach (var providerUser in providerUsers)
            {
                if (providerUser.Status != ProviderUserStatusType.Invited || providerUser.ProviderId != providerId)
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

            if (!CoreHelpers.TokenIsValid("ProviderUserInvite", _dataProtector, token, user.Email, providerUser.Id, _globalSettings))
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
                !await HasConfirmedProviderAdminExceptAsync(user.ProviderId, new[] {user.Id}))
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

            var providerOrganization = new ProviderOrganization
            {
                ProviderId = providerId,
                OrganizationId = organizationId,
                Key = key,
            };

            await _providerOrganizationRepository.CreateAsync(providerOrganization);
        }

        public async Task<ProviderOrganization> CreateOrganizationAsync(Guid providerId, OrganizationSignup organizationSignup, User user)
        {
            var (organization, _) = await _organizationService.SignUpAsync(organizationSignup, true);

            var providerOrganization = new ProviderOrganization
            {
                ProviderId = providerId,
                OrganizationId = organization.Id,
                Key = organizationSignup.OwnerKey,
            };

            await _providerOrganizationRepository.CreateAsync(providerOrganization);
            return providerOrganization;
        }

        public async Task RemoveOrganization(Guid providerId, Guid providerOrganizationId, Guid removingUserId)
        {
            var providerOrganization = await _providerOrganizationRepository.GetByIdAsync(providerOrganizationId);

            if (providerOrganization == null || providerOrganization.ProviderId != providerId)
            {
                throw new BadRequestException("Invalid organization");
            }
            
            if (!await _organizationService.HasConfirmedOwnersExceptAsync(providerOrganization.OrganizationId, new Guid[] {}))
            {
                throw new BadRequestException("Organization needs to have at least one confirmed owner");
            }

            await _providerOrganizationRepository.DeleteAsync(providerOrganization);
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
    }
}
