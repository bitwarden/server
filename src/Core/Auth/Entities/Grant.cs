#nullable enable
using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Auth.Entities;

public class Grant
{
    public int Id { get; set; }
    [MaxLength(200)]
    public string? Key { get; set; }
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
