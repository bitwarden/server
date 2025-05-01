using System.ComponentModel.DataAnnotations;
using Bit.Core.Billing.Models.Api.Requests;

namespace Bit.Core.Billing.Premium.Requests;

public class PreviewIndividualInvoiceRequestBody
{
    [Required]
    public IndividualPasswordManagerRequestModel PasswordManager { get; set; }

    [Required]
    public TaxInformationRequestModel TaxInformation { get; set; }
}

public class IndividualPasswordManagerRequestModel
{
    [Range(0, int.MaxValue)]
    public int AdditionalStorage { get; set; }
}
