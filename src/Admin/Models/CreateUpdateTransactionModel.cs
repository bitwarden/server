using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities;
using Bit.Core.Enums;

namespace Bit.Admin.Models;

public class CreateUpdateTransactionModel : IValidatableObject
{
    public CreateUpdateTransactionModel() { }

    public CreateUpdateTransactionModel(Transaction transaction)
    {
        Edit = true;
        UserId = transaction.UserId;
        OrganizationId = transaction.OrganizationId;
        Amount = transaction.Amount;
        RefundedAmount = transaction.RefundedAmount;
        Refunded = transaction.Refunded.GetValueOrDefault();
        Details = transaction.Details;
        Date = transaction.CreationDate;
        PaymentMethod = transaction.PaymentMethodType;
        Gateway = transaction.Gateway;
        GatewayId = transaction.GatewayId;
        Type = transaction.Type;
    }

    public bool Edit { get; set; }

    [Display(Name = "User Id")]
    public Guid? UserId { get; set; }
    [Display(Name = "Organization Id")]
    public Guid? OrganizationId { get; set; }
    [Required]
    public decimal? Amount { get; set; }
    [Display(Name = "Refunded Amount")]
    public decimal? RefundedAmount { get; set; }
    public bool Refunded { get; set; }
    [Required]
    public string Details { get; set; }
    [Required]
    public DateTime? Date { get; set; }
    [Display(Name = "Payment Method")]
    public PaymentMethodType? PaymentMethod { get; set; }
    public GatewayType? Gateway { get; set; }
    [Display(Name = "Gateway Id")]
    public string GatewayId { get; set; }
    [Required]
    public TransactionType? Type { get; set; }


    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if ((!UserId.HasValue && !OrganizationId.HasValue) || (UserId.HasValue && OrganizationId.HasValue))
        {
            yield return new ValidationResult("Must provide either User Id, or Organization Id.");
        }
    }

    public Transaction ToTransaction(Guid? id = null)
    {
        return new Transaction
        {
            Id = id.GetValueOrDefault(),
            UserId = UserId,
            OrganizationId = OrganizationId,
            Amount = Amount.Value,
            RefundedAmount = RefundedAmount,
            Refunded = Refunded ? true : (bool?)null,
            Details = Details,
            CreationDate = Date.Value,
            PaymentMethodType = PaymentMethod,
            Gateway = Gateway,
            GatewayId = GatewayId,
            Type = Type.Value
        };
    }
}
