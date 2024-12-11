#nullable enable
using System.ComponentModel.DataAnnotations;
using Bit.Core.Auth.Models.Data;
using Duende.IdentityServer.Models;

namespace Bit.Core.Auth.Entities;

public class Grant : IGrant
{
    public Grant() { }

    public Grant(PersistedGrant pGrant)
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
    }

    public int Id { get; set; }

    [MaxLength(200)]
    public string Key { get; set; } = null!;

    [MaxLength(50)]
    public string Type { get; set; } = null!;

    [MaxLength(200)]
    public string? SubjectId { get; set; }

    [MaxLength(100)]
    public string? SessionId { get; set; }

    [MaxLength(200)]
    public string ClientId { get; set; } = null!;

    [MaxLength(200)]
    public string? Description { get; set; }
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public DateTime? ExpirationDate { get; set; }
    public DateTime? ConsumedDate { get; set; }
    public string Data { get; set; } = null!;
}
