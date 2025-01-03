using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Billing.Models.Api.Requests.Accounts;

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
