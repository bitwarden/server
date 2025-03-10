using System.ComponentModel.DataAnnotations;
using Bit.Core.Billing.Enums;

namespace Bit.Core.Billing.Models.Api.Requests.Organizations;

public class PreviewOrganizationInvoiceRequestBody
{
    public Guid OrganizationId { get; set; }

    [Required]
    public OrganizationPasswordManagerRequestModel PasswordManager { get; set; }

    public SecretsManagerRequestModel SecretsManager { get; set; }

    [Required]
    public TaxInformationRequestModel TaxInformation { get; set; }
}

public class OrganizationPasswordManagerRequestModel
{
    public PlanType Plan { get; set; }

    [Range(0, int.MaxValue)]
    public int Seats { get; set; }

    [Range(0, int.MaxValue)]
    public int AdditionalStorage { get; set; }
}

public class SecretsManagerRequestModel
{
    [Range(0, int.MaxValue)]
    public int Seats { get; set; }

    [Range(0, int.MaxValue)]
    public int AdditionalMachineAccounts { get; set; }
}
