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
        private readonly IPushService _pushService;

        public CipherService(
            ICipherRepository cipherRepository,
            IFolderRepository folderRepository,
            IUserRepository userRepository,
            IOrganizationRepository organizationRepository,
            IOrganizationUserRepository organizationUserRepository,
            ISubvaultUserRepository subvaultUserRepository,
            IPushService pushService)
        {
            _cipherRepository = cipherRepository;
            _folderRepository = folderRepository;
            _userRepository = userRepository;
            _organizationRepository = organizationRepository;
            _organizationUserRepository = organizationUserRepository;
            _subvaultUserRepository = subvaultUserRepository;
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

        public async Task UpdatePartialAsync(Guid cipherId, Guid savingUserId, Guid? folderId, bool favorite)
        {
            if(!(await UserCanPartialEditAsync(cipherId, savingUserId)))
            {
                throw new BadRequestException("Cannot edit.");
            }

            await _cipherRepository.UpdatePartialAsync(cipherId, savingUserId, folderId, favorite);
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

        public async Task MoveSubvaultAsync(Cipher cipher, Guid organizationId, IEnumerable<Guid> subvaultIds, Guid movingUserId)
        {
            if(cipher.Id == default(Guid))
            {
                throw new BadRequestException(nameof(cipher.Id));
            }

            if(cipher.OrganizationId.HasValue)
            {
                throw new BadRequestException("Already belongs to an organization.");
            }

            if(!cipher.UserId.HasValue || cipher.UserId.Value != movingUserId)
            {
                throw new NotFoundException();
            }

            // We do not need to check if the user belongs to this organization since this call will return no subvaults
            // and therefore be caught by the .Any() check below.
            var subvaultUserDetails = await _subvaultUserRepository.GetPermissionsByUserIdAsync(movingUserId, subvaultIds,
                organizationId);

            var writeableSubvaults = subvaultUserDetails.Where(s => !s.ReadOnly).Select(s => s.SubvaultId);
            if(!writeableSubvaults.Any())
            {
                throw new BadRequestException("No subvaults.");
            }

            cipher.UserId = null;
            cipher.OrganizationId = organizationId;
            cipher.RevisionDate = DateTime.UtcNow;
            await _cipherRepository.ReplaceAsync(cipher, writeableSubvaults);

            // push
            //await _pushService.PushSyncCipherUpdateAsync(cipher);
        }

        public async Task ImportCiphersAsync(
            List<Folder> folders,
            List<CipherDetails> ciphers,
            IEnumerable<KeyValuePair<int, int>> folderRelationships)
        {
            // create all the folders
            var folderTasks = new List<Task>();
            foreach(var folder in folders)
            {
                folderTasks.Add(_folderRepository.CreateAsync(folder));
            }
            await Task.WhenAll(folderTasks);

            // associate the newly created folders to the ciphers
            foreach(var relationship in folderRelationships)
            {
                var cipher = ciphers.ElementAtOrDefault(relationship.Key);
                var folder = folders.ElementAtOrDefault(relationship.Value);

                if(cipher == null || folder == null)
                {
                    continue;
                }

                //cipher.FolderId = folder.Id;
            }

            // create all the ciphers
            await _cipherRepository.CreateAsync(ciphers);

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

        private Task<bool> UserCanPartialEditAsync(Guid cipherId, Guid userId)
        {
            // TODO: implement

            return Task.FromResult(true);
            //return await _subvaultUserRepository.GetCanEditByUserIdCipherIdAsync(userId, cipherId);
        }
    }
}
