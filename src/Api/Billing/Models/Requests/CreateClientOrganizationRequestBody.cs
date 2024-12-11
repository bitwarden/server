using System.ComponentModel.DataAnnotations;
using Bit.Api.Utilities;
using Bit.Core.Billing.Enums;

namespace Bit.Api.Billing.Models.Requests;

public class CreateClientOrganizationRequestBody
{
    [Required(ErrorMessage = "'name' must be provided")]
    public string Name { get; set; }

    [Required(ErrorMessage = "'ownerEmail' must be provided")]
    public string OwnerEmail { get; set; }

    [EnumMatches<PlanType>(
        PlanType.TeamsMonthly,
        PlanType.EnterpriseMonthly,
        PlanType.EnterpriseAnnually,
        ErrorMessage = "'planType' must be Teams (Monthly), Enterprise (Monthly) or Enterprise (Annually)"
    )]
    public PlanType PlanType { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "'seats' must be greater than 0")]
    public int Seats { get; set; }

    [Required(ErrorMessage = "'key' must be provided")]
    public string Key { get; set; }

    [Required(ErrorMessage = "'keyPair' must be provided")]
    public KeyPairRequestBody KeyPair { get; set; }

    [Required(ErrorMessage = "'collectionName' must be provided")]
    public string CollectionName { get; set; }
}
