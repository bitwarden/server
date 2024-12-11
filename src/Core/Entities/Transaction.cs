using System.ComponentModel.DataAnnotations;
using Bit.Core.Enums;
using Bit.Core.Utilities;

#nullable enable

namespace Bit.Core.Entities;

public class Transaction : ITableObject<Guid>
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public Guid? OrganizationId { get; set; }
    public TransactionType Type { get; set; }
    public decimal Amount { get; set; }
    public bool? Refunded { get; set; }
    public decimal? RefundedAmount { get; set; }

    [MaxLength(100)]
    public string? Details { get; set; }
    public PaymentMethodType? PaymentMethodType { get; set; }
    public GatewayType? Gateway { get; set; }

    [MaxLength(50)]
    public string? GatewayId { get; set; }
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public Guid? ProviderId { get; set; }

    public void SetNewId()
    {
        Id = CoreHelpers.GenerateComb();
    }
}
