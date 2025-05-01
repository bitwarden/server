namespace Bit.Core.Billing.Tax.Responses;

public record PreviewInvoiceResponseModel(
    decimal EffectiveTaxRate,
    decimal TaxableBaseAmount,
    decimal TaxAmount,
    decimal TotalAmount);
