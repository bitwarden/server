using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Bit.Core.Enums;
using Bit.Core.Enums.Provider;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business.Provider;
using Bit.Core.Models.Table.Provider;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.DataProtection;

namespace Bit.Core.Services
{
    public class ProviderService : IProviderService
    {
        private readonly IDataProtector _dataProtector;
        private readonly IMailService _mailService;
        private readonly IEventService _eventService;
        private readonly GlobalSettings _globalSettings;
        private readonly IProviderRepository _providerRepository;
        private readonly IProviderUserRepository _providerUserRepository;
        private readonly IUserService _userService;

        public ProviderService(IProviderRepository providerRepository, IProviderUserRepository providerUserRepository,
            IUserService userService, IMailService mailService, IDataProtectionProvider dataProtectionProvider,
            IEventService eventService, GlobalSettings globalSettings)
        {
            _providerRepository = providerRepository;
            _providerUserRepository = providerUserRepository;
            _userService = userService;
            _mailService = mailService;
            _eventService = eventService;
            _globalSettings = globalSettings;
            _dataProtector = dataProtectionProvider.CreateProtector("ProviderServiceDataProtector");
        }

        public async Task CreateAsync(Guid ownerUserId)
        {
            var owner = await _userService.GetUserByIdAsync(ownerUserId);
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
            
            var token = _dataProtector.Protect($"ProviderSetupInvite {provider.Id} {owner.Email} {CoreHelpers.ToEpocMilliseconds(DateTime.UtcNow)}");
            await _mailService.SendProviderSetupInviteEmailAsync(provider, token, owner.Email);
        }

        public async Task CompleteSetupAsync(Provider provider, Guid ownerUserId, string token, string key)
        {
            var owner = await _userService.GetUserByIdAsync(ownerUserId);
            if (owner == null)
            {
                throw new BadRequestException("Invalid owner.");
            }

            if (!CoreHelpers.TokenIsValid("ProviderSetupInvite", _dataProtector, token, owner.Email, provider.Id, _globalSettings))
            {
                throw new BadRequestException("Invalid token.");
            }
            
            await _providerRepository.UpsertAsync(provider);
            
            var providerUser = new ProviderUser
            {
                ProviderId = provider.Id,
                UserId = owner.Id,
                Key = key,
                Status = ProviderUserStatusType.Confirmed,
                Type = ProviderUserType.Administrator,
            };

            await _providerUserRepository.CreateAsync(providerUser);
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

            await ValidateProviderUserUpdatePermissionsAsync(invitingUserId, providerId, invite.Type, null);

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

                if (invite.Permissions != null)
                {
                    providerUser.Permissions = JsonSerializer.Serialize(invite.Permissions, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    });
                }

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

        public Task<ProviderUser> AcceptUserAsync(string orgIdentifier, Guid acceptingUserId, string token) => throw new NotImplementedException();

        public Task<ProviderUser> ConfirmUsersAsync(Guid providerId, Dictionary<Guid, string> keys, Guid confirmingUserId) => throw new NotImplementedException();

        public Task SaveUserAsync(ProviderUser user, Guid savingUserId) => throw new NotImplementedException();

        public Task UpdateUserAsync(ProviderUser user, Guid savingUserId) => throw new NotImplementedException();

        public Task DeleteUsersAsync(Guid providerId, IEnumerable<Guid> providerUserIds, Guid? deletingUserId) => throw new NotImplementedException();

        public Task AddOrganization(Guid providerId, Guid organizationId, Guid addingUserId, string key) => throw new NotImplementedException();

        public Task RemoveOrganization(Guid providerOrganizationId, Guid removingUserId) => throw new NotImplementedException();
        
        private async Task SendInviteAsync(ProviderUser providerUser, Provider provider)
        {
            var nowMillis = CoreHelpers.ToEpocMilliseconds(DateTime.UtcNow);
            var token = _dataProtector.Protect(
                $"ProviderUserInvite {providerUser.Id} {providerUser.Email} {nowMillis}");
            await _mailService.SendProviderInviteEmailAsync(provider.Name, providerUser, token);
        }
        
        private async Task ValidateProviderUserUpdatePermissionsAsync(Guid loggedInUserId, Guid providerId,
            ProviderUserType newType, ProviderUserType? oldType)
        {
            return;
        }
    }
}
