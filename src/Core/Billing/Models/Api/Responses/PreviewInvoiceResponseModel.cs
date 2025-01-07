namespace Bit.Core.Billing.Models.Api.Responses;

public record PreviewInvoiceResponseModel(
    decimal EffectiveTaxRate,
    decimal TaxableBaseAmount,
    decimal TaxAmount,
    decimal TotalAmount);
