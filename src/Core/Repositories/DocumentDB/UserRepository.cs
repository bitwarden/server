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
            await DocumentDBHelpers.ExecuteWithRetryAsync(async () =>
            {
                await Client.ExecuteStoredProcedureAsync<Domains.User>(
                    ResolveSprocIdLink(user, "replaceUserAndDirtyCiphers"),
                    user);
            });
        }

        public override async Task DeleteAsync(Domains.User user)
        {
            await DeleteByIdAsync(user.Id);
        }

        public override async Task DeleteByIdAsync(string id)
        {
            await DocumentDBHelpers.ExecuteWithRetryAsync(async () =>
            {
                while(true)
                {
                    StoredProcedureResponse<dynamic> sprocResponse = await Client.ExecuteStoredProcedureAsync<dynamic>(
                        ResolveSprocIdLink(id, "bulkDelete"),
                        string.Format("SELECT * FROM c WHERE c.id = '{0}' OR c.UserId = '{0}'", id));

                    if(!(bool)sprocResponse.Response.continuation)
                    {
                        break;
                    }
                }
            });
        }
    }
}
