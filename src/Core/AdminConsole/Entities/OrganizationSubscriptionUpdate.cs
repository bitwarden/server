using Bit.Core.Entities;
using Bit.Core.Utilities;

namespace Bit.Core.AdminConsole.Entities;

public class OrganizationSubscriptionUpdate : ITableObject<Guid>
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public DateTime? SeatsLastUpdated { get; set; }
    public int SyncAttempts { get; set; }

    public void SetNewId()
    {
        if (Id == Guid.Empty)
        {
            Id = CoreHelpers.GenerateComb();
        }
    }
}
