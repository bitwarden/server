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

        public CipherService(
            ICipherRepository cipherRepository)
        {
            _cipherRepository = cipherRepository;
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
        }
    }
}
