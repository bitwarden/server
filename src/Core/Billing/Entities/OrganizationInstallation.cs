using Bit.Core.Entities;
using Bit.Core.Utilities;

namespace Bit.Core.Billing.Entities;

#nullable enable

public class OrganizationInstallation : ITableObject<Guid>
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }
    public Guid InstallationId { get; set; }
    public DateTime CreationDate { get; internal set; } = DateTime.UtcNow;
    public DateTime? RevisionDate { get; set; }

    public void SetNewId()
    {
        if (Id == default)
        {
            Id = CoreHelpers.GenerateComb();
        }
    }
}
