// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Billing.Tax.Requests;

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
