using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Billing.Models.Requests;

public class UpdatePaymentMethodRequestBody
{
    [Required]
    public TokenizedPaymentSourceRequestBody PaymentSource { get; set; }

    [Required]
    public TaxInformationRequestBody TaxInformation { get; set; }
}
