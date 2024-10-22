namespace Bit.Api.Billing.Models.Requests;

public class CalculateTaxRequestModel
{
    public decimal Amount { get; set; }

    public string PostalCode { get; set; }

    public string Country { get; set; }
}
