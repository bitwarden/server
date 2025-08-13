#nullable enable
using System.ComponentModel.DataAnnotations;
using Bit.Core.Billing.Enums;

namespace Bit.Api.Billing.Models.Requests;

public class PreviewTaxAmountForOrganizationTrialRequestBody
{
    [Required]
    public PlanType PlanType { get; set; }

    [Required]
    public ProductType ProductType { get; set; }

    [Required] public TaxInformationDTO TaxInformation { get; set; } = null!;

    public class TaxInformationDTO
    {
        [Required]
        public string Country { get; set; } = null!;

        [Required]
        public string PostalCode { get; set; } = null!;

        public string? TaxId { get; set; }
    }
}
