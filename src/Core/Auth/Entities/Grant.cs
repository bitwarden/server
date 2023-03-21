using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Entities;

public class Grant
{
    [MaxLength(200)]
    public string Key { get; set; }
    [MaxLength(50)]
    public string Type { get; set; }
    [MaxLength(200)]
    public string SubjectId { get; set; }
    [MaxLength(100)]
    public string SessionId { get; set; }
    [MaxLength(200)]
    public string ClientId { get; set; }
    [MaxLength(200)]
    public string Description { get; set; }
    public DateTime CreationDate { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public DateTime? ConsumedDate { get; set; }
    public string Data { get; set; }
}
