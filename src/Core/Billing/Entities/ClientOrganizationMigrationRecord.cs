using System.ComponentModel.DataAnnotations;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Utilities;

#nullable enable

namespace Bit.Core.Billing.Entities;

public class ClientOrganizationMigrationRecord : ITableObject<Guid>
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid ProviderId { get; set; }
    public PlanType PlanType { get; set; }
    public int Seats { get; set; }
    public short? MaxStorageGb { get; set; }

    [MaxLength(50)]
    public string GatewayCustomerId { get; set; } = null!;

    [MaxLength(50)]
    public string GatewaySubscriptionId { get; set; } = null!;
    public DateTime? ExpirationDate { get; set; }
    public int? MaxAutoscaleSeats { get; set; }
    public OrganizationStatusType Status { get; set; }

    public void SetNewId()
    {
        if (Id == default)
        {
            Id = CoreHelpers.GenerateComb();
        }
    }
}
