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
        private readonly IFolderRepository _folderRepository;
        private readonly ICipherRepository _cipherRepository;

        public CipherService(
            IFolderRepository folderRepository,
            ICipherRepository cipherRepository)
        {
            _folderRepository = folderRepository;
            _cipherRepository = cipherRepository;
        }

        public async Task ImportCiphersAsync(
            List<Folder> folders,
            List<Site> sites,
            IEnumerable<KeyValuePair<int, int>> siteRelationships)
        {
            // create all the folders
            var folderTasks = new List<Task>();
            foreach(var folder in folders)
            {
                folderTasks.Add(_folderRepository.CreateAsync(folder));
            }
            await Task.WhenAll(folderTasks);

            // associate the newly created folders to the sites
            foreach(var relationship in siteRelationships)
            {
                var site = sites.ElementAtOrDefault(relationship.Key);
                var folder = folders.ElementAtOrDefault(relationship.Value);

                if(site == null || folder == null)
                {
                    continue;
                }

                site.FolderId = folder.Id;
            }

            // create all the sites
            await _cipherRepository.CreateAsync(sites);
        }
    }
}
