using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Domains;
using Bit.Core.Repositories;

namespace Bit.Core.Services
{
    public class CipherService : ICipherService
    {
        private readonly ICipherRepository _cipherRepository;
        private readonly IShareRepository _shareRepository;
        private readonly IUserRepository _userRepository;
        private readonly IPushService _pushService;

        public CipherService(
            ICipherRepository cipherRepository,
            IShareRepository shareRepository,
            IUserRepository userRepository,
            IPushService pushService)
        {
            _cipherRepository = cipherRepository;
            _shareRepository = shareRepository;
            _userRepository = userRepository;
            _pushService = pushService;
        }

        public async Task SaveAsync(Cipher cipher)
        {
            if(cipher.Id == default(Guid))
            {
                await _cipherRepository.CreateAsync(cipher);

                // push
                await _pushService.PushSyncCipherCreateAsync(cipher);
            }
            else
            {
                cipher.RevisionDate = DateTime.UtcNow;
                await _cipherRepository.ReplaceAsync(cipher);

                // push
                await _pushService.PushSyncCipherUpdateAsync(cipher);
            }
        }

        public async Task DeleteAsync(Cipher cipher)
        {
            await _cipherRepository.DeleteAsync(cipher);

            // push
            await _pushService.PushSyncCipherDeleteAsync(cipher);
        }

        public async Task ImportCiphersAsync(
            List<Cipher> folders,
            List<Cipher> ciphers,
            IEnumerable<KeyValuePair<int, int>> folderRelationships)
        {
            // create all the folders
            var folderTasks = new List<Task>();
            foreach(var folder in folders)
            {
                folderTasks.Add(_cipherRepository.CreateAsync(folder));
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

                cipher.FolderId = folder.Id;
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

        public async Task ShareAsync(Share share, string email)
        {
            // TODO: Make sure share does not already exist between these two users.

            var user = await _userRepository.GetByEmailAsync(email);
            if(user == null)
            {
                return;
            }

            share.UserId = user.Id;

            // TODO: Permissions and status
            share.ReadOnly = false;
            share.Status = Enums.ShareStatusType.Accepted;

            await _shareRepository.CreateAsync(share);
        }
    }
}
