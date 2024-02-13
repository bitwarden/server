using System.ComponentModel.DataAnnotations;

namespace Bit.Infrastructure.EntityFramework.Models;

public class Cache
{
    [StringLength(449)]
    public string Id { get; set; }
    public byte[] Value { get; set; }
    public DateTimeOffset ExpiresAtTime { get; set; }
    public long? SlidingExpirationInSeconds { get; set; }
    public DateTimeOffset? AbsoluteExpiration { get; set; }
}
