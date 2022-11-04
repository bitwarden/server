using System.ComponentModel.DataAnnotations;
using Bit.Core.Utilities;

namespace Bit.Core.Entities;

public class OrganizationDomain : ITableObject<Guid>
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Txt { get; set; }
    [MaxLength(255)]
    public string DomainName { get; set; }
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public DateTime? VerifiedDate { get; set; }
    public DateTime NextRunDate { get; set; }
    public int NextRunCount { get; set; }
    public bool Active { get; set; }
    public void SetNewId() => Id = CoreHelpers.GenerateComb();
}
