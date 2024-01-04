using Duende.IdentityServer.Models;
using Newtonsoft.Json;

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

    [JsonProperty("id")]
    public string Key { get; set; }
    [JsonProperty("typ")]
    public string Type { get; set; }
    [JsonProperty("sub")]
    public string SubjectId { get; set; }
    [JsonProperty("sid")]
    public string SessionId { get; set; }
    [JsonProperty("cid")]
    public string ClientId { get; set; }
    [JsonProperty("des")]
    public string Description { get; set; }
    [JsonProperty("cre")]
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    [JsonProperty("exp")]
    public DateTime? ExpirationDate { get; set; }
    [JsonProperty("con")]
    public DateTime? ConsumedDate { get; set; }
    [JsonProperty("data")]
    public string Data { get; set; }
    // https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/how-to-time-to-live?tabs=dotnet-sdk-v3#set-time-to-live-on-an-item-using-an-sdk
    [JsonProperty("ttl")]
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
