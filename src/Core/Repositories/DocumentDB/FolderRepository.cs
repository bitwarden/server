using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Documents.Client;
using Bit.Core.Domains;
using Bit.Core.Enums;
using Bit.Core.Repositories.DocumentDB.Utilities;
using Microsoft.Azure.Documents;

namespace Bit.Core.Repositories.DocumentDB
{
    public class FolderRepository : Repository<Folder>, IFolderRepository
    {
        public FolderRepository(DocumentClient client, string databaseId)
            : base(client, databaseId)
        { }

        public async Task<Folder> GetByIdAsync(string id, string userId)
        {
            ResourceResponse<Document> doc = null;
            var docLink = ResolveDocumentIdLink(userId, id);

            await DocumentDBHelpers.ExecuteWithRetryAsync(async () =>
            {
                doc = await Client.ReadDocumentAsync(docLink);
            });

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

        public async Task<ICollection<Folder>> GetManyByUserIdAsync(string userId)
        {
            IEnumerable<Folder> docs = null;
            await DocumentDBHelpers.ExecuteWithRetryAsync(() =>
            {
                docs = Client.CreateDocumentQuery<Folder>(DatabaseUri, null, userId)
                    .Where(d => d.Type == Cipher.TypeValue && d.CipherType == CipherType.Folder && d.UserId == userId).AsEnumerable();

                return Task.FromResult(0);
            });

            return docs.ToList();
        }

        public async Task<ICollection<Folder>> GetManyByUserIdAsync(string userId, bool dirty)
        {
            IEnumerable<Folder> docs = null;
            await DocumentDBHelpers.ExecuteWithRetryAsync(() =>
            {
                docs = Client.CreateDocumentQuery<Folder>(DatabaseUri, null, userId)
                    .Where(d => d.Type == Cipher.TypeValue && d.CipherType == CipherType.Folder && d.UserId == userId && d.Dirty == dirty).AsEnumerable();

                return Task.FromResult(0);
            });

            return docs.ToList();
        }
    }
}
