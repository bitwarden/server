using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities;
using Bit.Core.Utilities;

#nullable enable

namespace Bit.Core.Billing.Entities;

public class ProviderInvoiceItem : ITableObject<Guid>
{
    public Guid Id { get; set; }
    public Guid ProviderId { get; set; }

    [MaxLength(50)]
    public string InvoiceId { get; set; } = null!;

    [MaxLength(50)]
    public string? InvoiceNumber { get; set; }
    public Guid? ClientId { get; set; }

    [MaxLength(50)]
    public string ClientName { get; set; } = null!;

    [MaxLength(50)]
    public string PlanName { get; set; } = null!;
    public int AssignedSeats { get; set; }
    public int UsedSeats { get; set; }
    public decimal Total { get; set; }
    public DateTime Created { get; set; } = DateTime.UtcNow;

    public void SetNewId()
    {
        if (Id == default)
        {
            Id = CoreHelpers.GenerateComb();
        }
    }
}
