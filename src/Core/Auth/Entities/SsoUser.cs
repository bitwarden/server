// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities;

namespace Bit.Core.Auth.Entities;

public class SsoUser : ITableObject<long>
{
    public long Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? OrganizationId { get; set; }
    [MaxLength(300)]
    public string ExternalId { get; set; }
    public DateTime CreationDate { get; internal set; } = DateTime.UtcNow;

    public void SetNewId()
    {
        // int will be auto-populated
        Id = 0;
    }
}
