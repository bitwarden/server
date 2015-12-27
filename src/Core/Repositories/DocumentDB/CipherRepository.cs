using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Documents.Client;
using Bit.Core.Domains;
using Bit.Core.Repositories.DocumentDB.Utilities;

namespace Bit.Core.Repositories.DocumentDB
{
    public class CipherRepository : BaseRepository<Cipher>, ICipherRepository
    {
        public CipherRepository(DocumentClient client, string databaseId, string documentType = null)
            : base(client, databaseId, documentType)
        { }

        public async Task UpdateDirtyCiphersAsync(IEnumerable<dynamic> ciphers)
        {
            await DocumentDBHelpers.QueryWithRetryAsync(async () =>
            {
                // Make sure we are dealing with cipher types since we accept any via dynamic.
                var cleanedCiphers = ciphers.Where(c => c is Cipher);
                if(cleanedCiphers.Count() == 0)
                {
                    return;
                }

                var userId = ((Cipher)cleanedCiphers.First()).UserId;
                StoredProcedureResponse<int> sprocResponse = await Client.ExecuteStoredProcedureAsync<int>(
                    ResolveSprocIdLink(userId, "bulkUpdateDirtyCiphers"),
                    // Do sets of 50. Recursion will handle the rest below.
                    cleanedCiphers.Take(50),
                    userId);

                var replacedCount = sprocResponse.Response;
                if(replacedCount != cleanedCiphers.Count())
                {
                    await UpdateDirtyCiphersAsync(cleanedCiphers.Skip(replacedCount));
                }
            });
        }

        public async Task CreateAsync(IEnumerable<dynamic> ciphers)
        {
            await DocumentDBHelpers.QueryWithRetryAsync(async () =>
            {
                // Make sure we are dealing with cipher types since we accept any via dynamic.
                var cleanedCiphers = ciphers.Where(c => c is Cipher);
                if(cleanedCiphers.Count() == 0)
                {
                    return;
                }

                var userId = ((Cipher)cleanedCiphers.First()).UserId;
                StoredProcedureResponse<int> sprocResponse = await Client.ExecuteStoredProcedureAsync<int>(
                    ResolveSprocIdLink(userId, "bulkCreate"),
                    // Do sets of 50. Recursion will handle the rest below.
                    cleanedCiphers.Take(50));

                var createdCount = sprocResponse.Response;
                if(createdCount != cleanedCiphers.Count())
                {
                    await CreateAsync(cleanedCiphers.Skip(createdCount));
                }
            });
        }
    }
}
