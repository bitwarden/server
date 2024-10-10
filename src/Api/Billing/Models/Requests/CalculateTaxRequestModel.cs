namespace Bit.Api.Billing.Models.Requests;

public class CalculateTaxRequestModel
{
    public decimal Amount { get; set; }

    public string BillingAddressPostalCode { get; set; }

    public string BillingAddressCountry { get; set; }
}
