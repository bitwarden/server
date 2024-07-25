using System.ComponentModel.DataAnnotations;

#nullable enable

namespace Bit.Infrastructure.EntityFramework.Models;

public class Cache
{
    [StringLength(449)]
    public required string Id { get; set; }
    public byte[] Value { get; set; } = null!;
    public DateTime ExpiresAtTime { get; set; }
    public long? SlidingExpirationInSeconds { get; set; }
    public DateTime? AbsoluteExpiration { get; set; }
}
