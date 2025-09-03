// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.ComponentModel.DataAnnotations;
using Bit.Core.Billing.Enums;
using Bit.Core.Enums;

namespace Bit.Core.Billing.Tax.Requests;

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

    public PlanSponsorshipType? SponsoredPlan { get; set; }

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
