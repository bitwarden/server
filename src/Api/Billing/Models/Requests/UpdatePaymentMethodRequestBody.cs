using System.ComponentModel.DataAnnotations;
using Bit.Core.Billing.Models;

namespace Bit.Api.Billing.Models.Requests;

public class UpdatePaymentMethodRequestBody
{
    [Required]
    public TokenizedPaymentSourceRequestBody PaymentSource { get; set; }

    [Required]
    public TaxInformationRequestBody TaxInformation { get; set; }

    public TokenizedPaymentSource GetTokenizedPaymentSource() => new (
        PaymentSource.Type,
        PaymentSource.Token);

    public TaxInformation GetTaxInformation() => new(
        TaxInformation.Country,
        TaxInformation.PostalCode,
        TaxInformation.TaxId,
        TaxInformation.Line1,
        TaxInformation.Line2,
        TaxInformation.City,
        TaxInformation.State);
}
