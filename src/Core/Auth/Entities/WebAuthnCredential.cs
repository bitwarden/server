using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities;
using Bit.Core.Utilities;

namespace Bit.Core.Auth.Entities;

public class WebAuthnCredential : ITableObject<Guid>
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    [MaxLength(50)]
    public string Name { get; set; }
    [MaxLength(256)]
    public string PublicKey { get; set; }
    [MaxLength(256)]
    public string DescriptorId { get; set; }
    public int Counter { get; set; }
    [MaxLength(20)]
    public string Type { get; set; }
    public Guid AaGuid { get; set; }
    public string UserKey { get; set; }
    public DateTime CreationDate { get; internal set; } = DateTime.UtcNow;
    public DateTime RevisionDate { get; internal set; } = DateTime.UtcNow;

    public void SetNewId()
    {
        Id = CoreHelpers.GenerateComb();
    }
}
