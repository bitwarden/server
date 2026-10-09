using System.ComponentModel.DataAnnotations;
using Bit.Core.AdminConsole.Enums.Partnerships;
using Bit.Core.AdminConsole.OrganizationFeatures.Partnerships;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Bit.Admin.AdminConsole.Models;

public class CreateOrganizationPartnershipModel
{
    [BindNever]
    public Guid OrganizationId { get; set; }

    [BindNever]
    public string? OrganizationName { get; set; }

    [Required]
    [StringLength(CreateOrganizationPartnershipRequest.NameMaxLength)]
    public string? Name { get; set; }

    [Display(Name = "Sponsored plan type")]
    [EnumDataType(typeof(SponsoredPlanType))]
    public SponsoredPlanType SponsoredPlanType { get; set; } = SponsoredPlanType.Premium;

    [Display(Name = "Binding mode")]
    public PartnershipBindingMode BindingMode { get; set; } = PartnershipBindingMode.Token;

    /// <summary>
    /// One origin per line, as entered in the form.
    /// </summary>
    [Display(Name = "Registered return origins")]
    public string? RegisteredReturnOrigins { get; set; }

    public CreateOrganizationPartnershipRequest ToRequest(Guid organizationId) => new()
    {
        OrganizationId = organizationId,
        Name = Name!.Trim(),
        SponsoredPlanType = SponsoredPlanType,
        BindingMode = BindingMode,
        RegisteredReturnOrigins = (RegisteredReturnOrigins ?? string.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
    };
}
