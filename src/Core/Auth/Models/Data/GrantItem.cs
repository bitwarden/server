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
    // https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/how-to-time-to-live?tabs=dotnet-sdk-v3#set-time-to-live-on-an-item-using-an-sdk
    public double ttl { get; set; }

    public void SetTtl()
    {
        if (ExpirationDate != null)
        {
            ttl = (ExpirationDate.Value - DateTime.UtcNow).TotalSeconds;
        }
    }
}
