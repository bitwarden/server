using System.Text.Json.Serialization;
using Bit.Core.Auth.Repositories.Cosmos;
using Duende.IdentityServer.Models;

namespace Bit.Core.Auth.Models.Data;

public class GrantItem : IGrant
{
    public GrantItem() { }

    public GrantItem(PersistedGrant pGrant)
    {
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
        SetTtl();
    }

    public GrantItem(IGrant g)
    {
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
        SetTtl();
    }

    [JsonPropertyName("id")]
    [JsonConverter(typeof(Base64IdStringConverter))]
    public string Key { get; set; }

    [JsonPropertyName("typ")]
    public string Type { get; set; }

    [JsonPropertyName("sub")]
    public string SubjectId { get; set; }

    [JsonPropertyName("sid")]
    public string SessionId { get; set; }

    [JsonPropertyName("cid")]
    public string ClientId { get; set; }

    [JsonPropertyName("des")]
    public string Description { get; set; }

    [JsonPropertyName("cre")]
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("exp")]
    public DateTime? ExpirationDate { get; set; }

    [JsonPropertyName("con")]
    public DateTime? ConsumedDate { get; set; }

    [JsonPropertyName("data")]
    public string Data { get; set; }

    // https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/how-to-time-to-live?tabs=dotnet-sdk-v3#set-time-to-live-on-an-item-using-an-sdk
    [JsonPropertyName("ttl")]
    public int Ttl { get; set; } = -1;

    public void SetTtl()
    {
        if (ExpirationDate != null)
        {
            var sec = (ExpirationDate.Value - DateTime.UtcNow).TotalSeconds;
            if (sec > 0)
            {
                Ttl = (int)sec;
            }
        }
    }
}
