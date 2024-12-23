namespace Bit.Core.Billing.Models;

public record PreviewInvoiceInfo(
    decimal EffectiveTaxRate,
    decimal TaxableBaseAmount,
    decimal TaxAmount,
    decimal TotalAmount);
