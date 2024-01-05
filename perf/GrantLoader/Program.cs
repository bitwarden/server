using System.Diagnostics;
using System.Security.Cryptography;
using Bit.Identity.IdentityServer;
using Duende.IdentityServer.Models;

namespace Bit.GrantLoader;

public class Program
{
    private static async Task Main(string[] args)
    {
        PersistedGrantStore _grantStore;
        var iterations = 1000;
        var checkpoint = 10;
        var store = "cosmos";

        if (args.Length > 0)
        {
            store = args[0];
        }
        if (args.Length > 1)
        {
            iterations = Convert.ToInt32(args[1]);
        }
        if (args.Length > 2)
        {
            checkpoint = Convert.ToInt32(args[2]);
        }

        if (store == "ats")
        {
            var atsConnectionStrings = new[] { "" };
            var atsGrantRepo = new Core.Auth.Repositories.TableStorage.GrantRepository(atsConnectionStrings);

            _grantStore = new PersistedGrantStore(
                atsGrantRepo,
                g => new Core.Auth.Models.Data.GrantTableEntity(g)
            );
        }
        else
        {
            var cosmosConnectionString = "";
            var cosmosGrantRepo = new Core.Auth.Repositories.Cosmos.GrantRepository(cosmosConnectionString);

            _grantStore = new PersistedGrantStore(
                cosmosGrantRepo,
                g => new Core.Auth.Models.Data.GrantItem(g)
            );
        }

        var sw = Stopwatch.StartNew();
        var checkpointSw = Stopwatch.StartNew();

        for (var i = 1; i <= iterations; i++)
        {
            if (i % checkpoint == 0)
            {
                Console.WriteLine("[{0}] Iteration: {1}, checkpoint runtime: {2}s, total runtime: {3}s",
                    DateTime.Now, i, Math.Round(checkpointSw.Elapsed.TotalSeconds, 2), Math.Round(sw.Elapsed.TotalSeconds, 2));
                checkpointSw.Restart();
            }

            var creationTime = DateTime.UtcNow;
            var guid = Guid.NewGuid();
            using var mySHA256 = SHA256.Create();
            var keyBytes = mySHA256.ComputeHash(guid.ToByteArray());
            var key = Convert.ToBase64String(keyBytes);

            var grant = new PersistedGrant
            {
                Key = key,
                SubjectId = guid.ToString(),
                CreationTime = creationTime,
                Data = "{\"CreationTime\":\"2023-11-08T11:45:40Z\",\"Lifetime\":2592001,\"ConsumedTime\":null,\"AccessToken\":{\"AllowedSigningAlgorithms\":[],\"Confirmation\":null,\"Audiences\":[],\"Issuer\":\"http://localhost\",\"CreationTime\":\"2023-11-08T11:45:40Z\",\"Lifetime\":3600,\"Type\":\"access_token\",\"ClientId\":\"web\",\"AccessTokenType\":0,\"Description\":null,\"Claims\":[{\"Type\":\"client_id\",\"Value\":\"web\",\"ValueType\":\"http://www.w3.org/2001/XMLSchema#string\"},{\"Type\":\"scope\",\"Value\":\"api\",\"ValueType\":\"http://www.w3.org/2001/XMLSchema#string\"},{\"Type\":\"scope\",\"Value\":\"offline_access\",\"ValueType\":\"http://www.w3.org/2001/XMLSchema#string\"},{\"Type\":\"sub\",\"Value\":\"" + guid.ToString() + "\",\"ValueType\":\"http://www.w3.org/2001/XMLSchema#string\"},{\"Type\":\"auth_time\",\"Value\":\"1699443940\",\"ValueType\":\"http://www.w3.org/2001/XMLSchema#integer64\"},{\"Type\":\"idp\",\"Value\":\"bitwarden\",\"ValueType\":\"http://www.w3.org/2001/XMLSchema#string\"},{\"Type\":\"amr\",\"Value\":\"Application\",\"ValueType\":\"http://www.w3.org/2001/XMLSchema#string\"},{\"Type\":\"premium\",\"Value\":\"false\",\"ValueType\":\"http://www.w3.org/2001/XMLSchema#boolean\"},{\"Type\":\"email\",\"Value\":\"jbaur+test@bitwarden.com\",\"ValueType\":\"http://www.w3.org/2001/XMLSchema#string\"},{\"Type\":\"email_verified\",\"Value\":\"false\",\"ValueType\":\"http://www.w3.org/2001/XMLSchema#boolean\"},{\"Type\":\"sstamp\",\"Value\":\"a4f2e0f3-e9f8-4014-b94e-b761d446a34b\",\"ValueType\":\"http://www.w3.org/2001/XMLSchema#string\"},{\"Type\":\"name\",\"Value\":\"Justin Test\",\"ValueType\":\"http://www.w3.org/2001/XMLSchema#string\"},{\"Type\":\"orgowner\",\"Value\":\"8ff8fefb-b035-436b-a25c-b04c00e30351\",\"ValueType\":\"http://www.w3.org/2001/XMLSchema#string\"},{\"Type\":\"accesssecretsmanager\",\"Value\":\"8ff8fefb-b035-436b-a25c-b04c00e30351\",\"ValueType\":\"http://www.w3.org/2001/XMLSchema#string\"},{\"Type\":\"device\",\"Value\":\"64b49c58-7768-4c30-8396-f851176daca6\",\"ValueType\":\"http://www.w3.org/2001/XMLSchema#string\"},{\"Type\":\"jti\",\"Value\":\"CE008210A8276DAB966D9C2607533E0C\",\"ValueType\":\"http://www.w3.org/2001/XMLSchema#string\"},{\"Type\":\"iat\",\"Value\":\"1699443940\",\"ValueType\":\"http://www.w3.org/2001/XMLSchema#integer64\"}],\"Version\":4},\"Version\":4}",
                Type = "refresh_token",
                ClientId = "web",
                Expiration = creationTime.AddDays(30),
            };

            // Read, should be nothing
            var noGrant = await _grantStore.GetAsync(key);

            // Create
            await _grantStore.StoreAsync(grant);

            // Read what we just created
            grant = await _grantStore.GetAsync(key);

            // Update expiration
            grant.Expiration = grant.Expiration!.Value.AddSeconds(60);
            await _grantStore.StoreAsync(grant);

            // Read again
            // grant = await _grantStore.GetAsync(key);
        }
    }
}
