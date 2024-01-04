using Bit.Core.Utilities;
using Duende.IdentityServer.Models;
using Microsoft.Azure.Cosmos.Table;

namespace Bit.Core.Auth.Models.Data;

public class GrantTableEntity : TableEntity, IGrant
{
    public GrantTableEntity() { }

    public GrantTableEntity(PersistedGrant pGrant)
    {
        var (partitionKey, rowKey) = CreateTableEntityKeys(pGrant.Key);
        PartitionKey = partitionKey;
        RowKey = rowKey;

        Key = pGrant.Key;
        Type = pGrant.Type;
        SubjectId = pGrant.SubjectId;
        SessionId = pGrant.SessionId;
        ClientId = pGrant.ClientId;
        Description = pGrant.Description;
        CreationDate = pGrant.CreationTime;
        ExpirationDate = pGrant.Expiration;
        ConsumedDate = pGrant.ConsumedTime;
        Data = pGrant.Data;
    }

    public GrantTableEntity(IGrant g)
    {
        var (partitionKey, rowKey) = CreateTableEntityKeys(g.Key);
        PartitionKey = partitionKey;
        RowKey = rowKey;

        Key = g.Key;
        Type = g.Type;
        SubjectId = g.SubjectId;
        SessionId = g.SessionId;
        ClientId = g.ClientId;
        Description = g.Description;
        CreationDate = g.CreationDate;
        ExpirationDate = g.ExpirationDate;
        ConsumedDate = g.ConsumedDate;
        Data = g.Data;
    }

    public string Key { get; set; }
    public string Type { get; set; }
    public string SubjectId { get; set; }
    public string SessionId { get; set; }
    public string ClientId { get; set; }
    public string Description { get; set; }
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public DateTime? ExpirationDate { get; set; }
    public DateTime? ConsumedDate { get; set; }
    public string Data { get; set; }

    public static (string partitionKey, string rowKey) CreateTableEntityKeys(string grantKey)
    {
        // TODO: How do we want to partition? Or do we? refs:
        // https://learn.microsoft.com/en-us/rest/api/storageservices/designing-a-scalable-partitioning-strategy-for-azure-table-storage
        // https://stackoverflow.com/questions/19671357/use-the-same-partitionkey-and-rowkey

        // Partition keys cannot contain certain special characters, change it to B64 URL format
        var keyBytes = Convert.FromBase64String(grantKey);
        var b64 = CoreHelpers.Base64UrlEncode(keyBytes);
        return (b64, string.Empty);
    }
}
