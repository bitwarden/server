using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Billing.Models.Api.Requests.Accounts;

public class PreviewInvoiceRequestBody
{
    [Required]
    public PasswordManagerRequestModel PasswordManager { get; set; }

    [Required]
    public TaxInformationRequestModel TaxInformation { get; set; }
}

public class PasswordManagerRequestModel
{
    [Range(0, int.MaxValue)]
    public int AdditionalStorage { get; set; }
}
