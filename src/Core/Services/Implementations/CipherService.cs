using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Models.Table;
using Bit.Core.Repositories;
using Core.Models.Data;
using Bit.Core.Exceptions;

namespace Bit.Core.Services
{
    public class CipherService : ICipherService
    {
        private readonly ICipherRepository _cipherRepository;
        private readonly IFolderRepository _folderRepository;
        private readonly IUserRepository _userRepository;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IOrganizationUserRepository _organizationUserRepository;
        private readonly ISubvaultUserRepository _subvaultUserRepository;
        private readonly ISubvaultCipherRepository _subvaultCipherRepository;
        private readonly IPushService _pushService;

        public CipherService(
            ICipherRepository cipherRepository,
            IFolderRepository folderRepository,
            IUserRepository userRepository,
            IOrganizationRepository organizationRepository,
            IOrganizationUserRepository organizationUserRepository,
            ISubvaultUserRepository subvaultUserRepository,
            ISubvaultCipherRepository subvaultCipherRepository,
            IPushService pushService)
        {
            _cipherRepository = cipherRepository;
            _folderRepository = folderRepository;
            _userRepository = userRepository;
            _organizationRepository = organizationRepository;
            _organizationUserRepository = organizationUserRepository;
            _subvaultUserRepository = subvaultUserRepository;
            _subvaultCipherRepository = subvaultCipherRepository;
            _pushService = pushService;
        }

        public async Task SaveAsync(CipherDetails cipher, Guid savingUserId)
        {
            if(!(await UserCanEditAsync(cipher, savingUserId)))
            {
                throw new BadRequestException("Not an admin.");
            }

            cipher.UserId = savingUserId;
            if(cipher.Id == default(Guid))
            {
                await _cipherRepository.CreateAsync(cipher);

                // push
                //await _pushService.PushSyncCipherCreateAsync(cipher);
            }
            else
            {
                cipher.RevisionDate = DateTime.UtcNow;
                await _cipherRepository.ReplaceAsync(cipher);

                // push
                //await _pushService.PushSyncCipherUpdateAsync(cipher);
            }
        }

        public async Task DeleteAsync(CipherDetails cipher, Guid deletingUserId)
        {
            if(!(await UserCanEditAsync(cipher, deletingUserId)))
            {
                throw new BadRequestException("Not an admin.");
            }

            await _cipherRepository.DeleteAsync(cipher);

            // push
            //await _pushService.PushSyncCipherDeleteAsync(cipher);
        }

        public async Task SaveFolderAsync(Folder folder)
        {
            if(folder.Id == default(Guid))
            {
                await _folderRepository.CreateAsync(folder);

                // push
                //await _pushService.PushSyncCipherCreateAsync(cipher);
            }
            else
            {
                folder.RevisionDate = DateTime.UtcNow;
                await _folderRepository.UpsertAsync(folder);

                // push
                //await _pushService.PushSyncCipherUpdateAsync(cipher);
            }
        }

        public async Task DeleteFolderAsync(Folder folder)
        {
            await _folderRepository.DeleteAsync(folder);

            // push
            //await _pushService.PushSyncCipherDeleteAsync(cipher);
        }

        public async Task ShareAsync(Cipher cipher, Guid organizationId, IEnumerable<Guid> subvaultIds, Guid sharingUserId)
        {
            if(cipher.Id == default(Guid))
            {
                throw new BadRequestException(nameof(cipher.Id));
            }

            if(cipher.OrganizationId.HasValue)
            {
                throw new BadRequestException("Already belongs to an organization.");
            }

            if(!cipher.UserId.HasValue || cipher.UserId.Value != sharingUserId)
            {
                throw new NotFoundException();
            }

            // Sproc will not save this UserId on the cipher. It is used limit scope of the subvaultIds.
            cipher.UserId = sharingUserId;
            cipher.OrganizationId = organizationId;
            cipher.RevisionDate = DateTime.UtcNow;
            await _cipherRepository.ReplaceAsync(cipher, subvaultIds);

            // push
            //await _pushService.PushSyncCipherUpdateAsync(cipher);
        }

        public async Task SaveSubvaultsAsync(Cipher cipher, IEnumerable<Guid> subvaultIds, Guid savingUserId, bool orgAdmin)
        {
            if(cipher.Id == default(Guid))
            {
                throw new BadRequestException(nameof(cipher.Id));
            }

            if(!cipher.OrganizationId.HasValue)
            {
                throw new BadRequestException("Cipher must belong to an organization.");
            }

            // The sprocs will validate that all subvaults belong to this org/user and that they have proper write permissions.
            if(orgAdmin)
            {
                await _subvaultCipherRepository.UpdateSubvaultsForAdminAsync(cipher.Id, cipher.OrganizationId.Value,
                    subvaultIds);
            }
            else
            {
                await _subvaultCipherRepository.UpdateSubvaultsAsync(cipher.Id, savingUserId, subvaultIds);
            }

            // push
            //await _pushService.PushSyncCipherUpdateAsync(cipher);
        }

        public async Task ImportCiphersAsync(
            List<Folder> folders,
            List<CipherDetails> ciphers,
            IEnumerable<KeyValuePair<int, int>> folderRelationships)
        {
            foreach(var cipher in ciphers)
            {
                cipher.SetNewId();

                if(cipher.UserId.HasValue && cipher.Favorite)
                {
                    cipher.Favorites = $"{{\"{cipher.UserId.ToString().ToUpperInvariant()}\":\"true\"}}";
                }
            }

            // Init. ids for folders
            foreach(var folder in folders)
            {
                folder.SetNewId();
            }

            // Create the folder associations based on the newly created folder ids
            foreach(var relationship in folderRelationships)
            {
                var cipher = ciphers.ElementAtOrDefault(relationship.Key);
                var folder = folders.ElementAtOrDefault(relationship.Value);

                if(cipher == null || folder == null)
                {
                    continue;
                }

                cipher.Folders = $"{{\"{cipher.UserId.ToString().ToUpperInvariant()}\":" +
                    $"\"{folder.Id.ToString().ToUpperInvariant()}\"}}";
            }

            // Create it all
            await _cipherRepository.CreateAsync(ciphers, folders);

            // push
            var userId = folders.FirstOrDefault()?.UserId ?? ciphers.FirstOrDefault()?.UserId;
            if(userId.HasValue)
            {
                await _pushService.PushSyncCiphersAsync(userId.Value);
            }
        }

        private async Task<bool> UserCanEditAsync(Cipher cipher, Guid userId)
        {
            if(!cipher.OrganizationId.HasValue && cipher.UserId.HasValue && cipher.UserId.Value == userId)
            {
                return true;
            }

            return await _subvaultUserRepository.GetCanEditByUserIdCipherIdAsync(userId, cipher.Id);
        }
    }
}
