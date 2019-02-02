using System;
using Bit.Core.Enums;
using Bit.Core.Utilities;

namespace Bit.Core.Models.Table
{
    public class Transaction : ITableObject<Guid>
    {
        public Guid Id { get; set; }
        public Guid? UserId { get; set; }
        public Guid? OrganizationId { get; set; }
        public TransactionType Type { get; set; }
        public decimal Amount { get; set; }
        public bool? Refunded { get; set; }
        public decimal? RefundedAmount { get; set; }
        public string Details { get; set; }
        public PaymentMethodType? PaymentMethodType { get; set; }
        public GatewayType? Gateway { get; set; }
        public string GatewayId { get; set; }
        public DateTime CreationDate { get; set; } = DateTime.UtcNow;

        public void SetNewId()
        {
            Id = CoreHelpers.GenerateComb();
        }
    }
}
