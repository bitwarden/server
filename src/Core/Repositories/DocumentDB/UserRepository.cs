using System;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.Repositories.DocumentDB.Utilities;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;

namespace Bit.Core.Repositories.DocumentDB
{
    public class UserRepository : Repository<Domains.User>, IUserRepository
    {
        public UserRepository(DocumentClient client, string databaseId)
            : base(client, databaseId)
        { }

        public override async Task<Domains.User> GetByIdAsync(string id)
        {
            return await GetByPartitionIdAsync(id);
        }

        public Task<Domains.User> GetByEmailAsync(string email)
        {
            var docs = Client.CreateDocumentQuery<Domains.User>(DatabaseUri, new FeedOptions { MaxItemCount = 1 })
                .Where(d => d.Type == Domains.User.TypeValue && d.Email == email).AsEnumerable();

            return Task.FromResult(docs.FirstOrDefault());
        }

        public async Task ReplaceAndDirtyCiphersAsync(Domains.User user)
        {
            await DocumentDBHelpers.QueryWithRetryAsync(async () =>
            {
                await Client.ExecuteStoredProcedureAsync<Domains.User>(ResolveSprocIdLink(user, "replaceUserAndDirtyCiphers"), user);
            });
        }

        public override async Task DeleteByIdAsync(string id)
        {
            await DeleteByPartitionIdAsync(id);
        }
    }
}
