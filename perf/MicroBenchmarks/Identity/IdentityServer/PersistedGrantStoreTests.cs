using BenchmarkDotNet.Attributes;
using Bit.Identity.IdentityServer;
using Bit.Infrastructure.Dapper.Auth.Repositories;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;

namespace Bit.MicroBenchmarks.Identity.IdentityServer;

[MemoryDiagnoser]
public class PersistedGrantStoreTests
{
    const string SQL = nameof(SQL);
    const string Cosmos = nameof(Cosmos);

    private readonly IPersistedGrantStore _sqlGrantStore;
    private readonly IPersistedGrantStore _cosmosGrantStore;
    private readonly PersistedGrant _updateGrant;

    private IPersistedGrantStore _grantStore = null!;

    // 1) "ConsumedTime"
    // 2) ""
    // 3) "Description"
    // 4) ""
    // 5) "SubjectId"
    // 6) "97f31e32-6e44-407f-b8ba-b04c00f51b41"
    // 7) "CreationTime"
    // 8) "638350407400000000"
    // 9) "Data"
    // 10) "{\"CreationTime\":\"2023-11-08T11:45:40Z\",\"Lifetime\":2592001,\"ConsumedTime\":null,\"AccessToken\":{\"AllowedSigningAlgorithms\":[],\"Confirmation\":null,\"Audiences\":[],\"Issuer\":\"http://localhost\",\"CreationTime\":\"2023-11-08T11:45:40Z\",\"Lifetime\":3600,\"Type\":\"access_token\",\"ClientId\":\"web\",\"AccessTokenType\":0,\"Description\":null,\"Claims\":[{\"Type\":\"client_id\",\"Value\":\"web\",\"ValueType\":\"http://www.w3.org/2001/XMLSchema#string\"},{\"Type\":\"scope\",\"Value\":\"api\",\"ValueType\":\"http://www.w3.org/2001/XMLSchema#string\"},{\"Type\":\"scope\",\"Value\":\"offline_access\",\"ValueType\":\"http://www.w3.org/2001/XMLSchema#string\"},{\"Type\":\"sub\",\"Value\":\"97f31e32-6e44-407f-b8ba-b04c00f51b41\",\"ValueType\":\"http://www.w3.org/2001/XMLSchema#string\"},{\"Type\":\"auth_time\",\"Value\":\"1699443940\",\"ValueType\":\"http://www.w3.org/2001/XMLSchema#integer64\"},{\"Type\":\"idp\",\"Value\":\"bitwarden\",\"ValueType\":\"http://www.w3.org/2001/XMLSchema#string\"},{\"Type\":\"amr\",\"Value\":\"Application\",\"ValueType\":\"http://www.w3.org/2001/XMLSchema#string\"},{\"Type\":\"premium\",\"Value\":\"false\",\"ValueType\":\"http://www.w3.org/2001/XMLSchema#boolean\"},{\"Type\":\"email\",\"Value\":\"jbaur+test@bitwarden.com\",\"ValueType\":\"http://www.w3.org/2001/XMLSchema#string\"},{\"Type\":\"email_verified\",\"Value\":\"false\",\"ValueType\":\"http://www.w3.org/2001/XMLSchema#boolean\"},{\"Type\":\"sstamp\",\"Value\":\"a4f2e0f3-e9f8-4014-b94e-b761d446a34b\",\"ValueType\":\"http://www.w3.org/2001/XMLSchema#string\"},{\"Type\":\"name\",\"Value\":\"Justin Test\",\"ValueType\":\"http://www.w3.org/2001/XMLSchema#string\"},{\"Type\":\"orgowner\",\"Value\":\"8ff8fefb-b035-436b-a25c-b04c00e30351\",\"ValueType\":\"http://www.w3.org/2001/XMLSchema#string\"},{\"Type\":\"accesssecretsmanager\",\"Value\":\"8ff8fefb-b035-436b-a25c-b04c00e30351\",\"ValueType\":\"http://www.w3.org/2001/XMLSchema#string\"},{\"Type\":\"device\",\"Value\":\"64b49c58-7768-4c30-8396-f851176daca6\",\"ValueType\":\"http://www.w3.org/2001/XMLSchema#string\"},{\"Type\":\"jti\",\"Value\":\"CE008210A8276DAB966D9C2607533E0C\",\"ValueType\":\"http://www.w3.org/2001/XMLSchema#string\"},{\"Type\":\"iat\",\"Value\":\"1699443940\",\"ValueType\":\"http://www.w3.org/2001/XMLSchema#integer64\"}],\"Version\":4},\"Version\":4}"
    // 11) "Type"
    // 12) "refresh_token"
    // 13) "SessionId"
    // 14) ""
    // 15) "ClientId"
    // 16) "web"

    public PersistedGrantStoreTests()
    {
        var sqlConnectionString = "YOUR CONNECTION STRING HERE";
        _sqlGrantStore = new PersistedGrantStore(
            new GrantRepository(sqlConnectionString, sqlConnectionString),
            g => new Bit.Core.Auth.Entities.Grant(g)
        );

        var cosmosConnectionString = "YOUR CONNECTION STRING HERE";
        _cosmosGrantStore = new PersistedGrantStore(
            new Bit.Core.Auth.Repositories.Cosmos.GrantRepository(cosmosConnectionString),
            g => new Bit.Core.Auth.Models.Data.GrantItem(g)
        );

        var creationTime = new DateTime(638350407400000000, DateTimeKind.Utc);
        _updateGrant = new PersistedGrant
        {
            Key = "i11JLqd7PE1yQltB2o5tRpfbMkpDPr+3w0Lc2Hx7kfE=",
            ConsumedTime = null,
            Description = null,
            SubjectId = "97f31e32-6e44-407f-b8ba-b04c00f51b41",
            CreationTime = creationTime,
            Data =
                "{\"CreationTime\":\"2023-11-08T11:45:40Z\",\"Lifetime\":2592001,\"ConsumedTime\":null,\"AccessToken\":{\"AllowedSigningAlgorithms\":[],\"Confirmation\":null,\"Audiences\":[],\"Issuer\":\"http://localhost\",\"CreationTime\":\"2023-11-08T11:45:40Z\",\"Lifetime\":3600,\"Type\":\"access_token\",\"ClientId\":\"web\",\"AccessTokenType\":0,\"Description\":null,\"Claims\":[{\"Type\":\"client_id\",\"Value\":\"web\",\"ValueType\":\"http://www.w3.org/2001/XMLSchema#string\"},{\"Type\":\"scope\",\"Value\":\"api\",\"ValueType\":\"http://www.w3.org/2001/XMLSchema#string\"},{\"Type\":\"scope\",\"Value\":\"offline_access\",\"ValueType\":\"http://www.w3.org/2001/XMLSchema#string\"},{\"Type\":\"sub\",\"Value\":\"97f31e32-6e44-407f-b8ba-b04c00f51b41\",\"ValueType\":\"http://www.w3.org/2001/XMLSchema#string\"},{\"Type\":\"auth_time\",\"Value\":\"1699443940\",\"ValueType\":\"http://www.w3.org/2001/XMLSchema#integer64\"},{\"Type\":\"idp\",\"Value\":\"bitwarden\",\"ValueType\":\"http://www.w3.org/2001/XMLSchema#string\"},{\"Type\":\"amr\",\"Value\":\"Application\",\"ValueType\":\"http://www.w3.org/2001/XMLSchema#string\"},{\"Type\":\"premium\",\"Value\":\"false\",\"ValueType\":\"http://www.w3.org/2001/XMLSchema#boolean\"},{\"Type\":\"email\",\"Value\":\"jbaur+test@bitwarden.com\",\"ValueType\":\"http://www.w3.org/2001/XMLSchema#string\"},{\"Type\":\"email_verified\",\"Value\":\"false\",\"ValueType\":\"http://www.w3.org/2001/XMLSchema#boolean\"},{\"Type\":\"sstamp\",\"Value\":\"a4f2e0f3-e9f8-4014-b94e-b761d446a34b\",\"ValueType\":\"http://www.w3.org/2001/XMLSchema#string\"},{\"Type\":\"name\",\"Value\":\"Justin Test\",\"ValueType\":\"http://www.w3.org/2001/XMLSchema#string\"},{\"Type\":\"orgowner\",\"Value\":\"8ff8fefb-b035-436b-a25c-b04c00e30351\",\"ValueType\":\"http://www.w3.org/2001/XMLSchema#string\"},{\"Type\":\"accesssecretsmanager\",\"Value\":\"8ff8fefb-b035-436b-a25c-b04c00e30351\",\"ValueType\":\"http://www.w3.org/2001/XMLSchema#string\"},{\"Type\":\"device\",\"Value\":\"64b49c58-7768-4c30-8396-f851176daca6\",\"ValueType\":\"http://www.w3.org/2001/XMLSchema#string\"},{\"Type\":\"jti\",\"Value\":\"CE008210A8276DAB966D9C2607533E0C\",\"ValueType\":\"http://www.w3.org/2001/XMLSchema#string\"},{\"Type\":\"iat\",\"Value\":\"1699443940\",\"ValueType\":\"http://www.w3.org/2001/XMLSchema#integer64\"}],\"Version\":4},\"Version\":4}",
            Type = "refresh_token",
            SessionId = null,
            ClientId = "web",
            Expiration = creationTime.AddHours(1),
        };
    }

    [Params(SQL, Cosmos)]
    public string StoreType { get; set; } = null!;

    [GlobalSetup]
    public void Setup()
    {
        if (StoreType == SQL)
        {
            _grantStore = _sqlGrantStore;
        }
        else if (StoreType == Cosmos)
        {
            _grantStore = _cosmosGrantStore;
        }
        else
        {
            throw new InvalidProgramException();
        }
    }

    [Benchmark]
    public async Task StoreAsync()
    {
        await _grantStore.StoreAsync(_updateGrant);
    }
}
