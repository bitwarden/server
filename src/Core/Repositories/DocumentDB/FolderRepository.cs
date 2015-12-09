using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Documents.Client;
using Bit.Core.Domains;
using Bit.Core.Enums;

namespace Bit.Core.Repositories.DocumentDB
{
    public class FolderRepository : Repository<Folder>, IFolderRepository
    {
        public FolderRepository(DocumentClient client, string databaseId)
            : base(client, databaseId)
        { }

        public async Task<Folder> GetByIdAsync(string id, string userId)
        {
            var doc = await Client.ReadDocumentAsync(ResolveDocumentIdLink(userId, id));
            if(doc?.Resource == null)
            {
                return null;
            }

            var folder = (Folder)((dynamic)doc.Resource);
            if(folder.UserId != userId)
            {
                return null;
            }

            return folder;
        }

        public Task<ICollection<Folder>> GetManyByUserIdAsync(string userId)
        {
            var docs = Client.CreateDocumentQuery<Folder>(DatabaseUri, null, userId)
                .Where(d => d.Type == Cipher.TypeValue && d.CipherType == CipherType.Folder && d.UserId == userId).AsEnumerable();

            return Task.FromResult<ICollection<Folder>>(docs.ToList());
        }

        public Task<ICollection<Folder>> GetManyByUserIdAsync(string userId, bool dirty)
        {
            var docs = Client.CreateDocumentQuery<Folder>(DatabaseUri, null, userId)
                .Where(d => d.Type == Cipher.TypeValue && d.CipherType == CipherType.Folder && d.UserId == userId && d.Dirty == dirty).AsEnumerable();

            return Task.FromResult<ICollection<Folder>>(docs.ToList());
        }
    }
}
