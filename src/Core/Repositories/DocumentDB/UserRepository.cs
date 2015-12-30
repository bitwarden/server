using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.Repositories.DocumentDB.Utilities;
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

        public async Task<Domains.User> GetByEmailAsync(string email)
        {
            IEnumerable<Domains.User> docs = null;
            await DocumentDBHelpers.ExecuteWithRetryAsync(() =>
            {
                docs = Client.CreateDocumentQuery<Domains.User>(DatabaseUri, new FeedOptions { MaxItemCount = 1 })
                    .Where(d => d.Type == Domains.User.TypeValue && d.Email == email).AsEnumerable();

                return Task.FromResult(0);
            });

            return docs.FirstOrDefault();
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
