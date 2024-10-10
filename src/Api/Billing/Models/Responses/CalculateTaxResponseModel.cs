namespace Bit.Api.Billing.Models.Responses;

public class CalculateTaxResponseModel
{
    public decimal SalesTaxAmount { get; set; }

    public decimal SalesTaxRate { get; set; }

    public decimal TaxableAmount { get; set; }

    public decimal TotalAmount { get; set; }
}
