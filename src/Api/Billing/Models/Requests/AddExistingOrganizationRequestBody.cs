using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Billing.Models.Requests;

public class AddExistingOrganizationRequestBody
{
    [Required(ErrorMessage = "'key' must be provided")]
    public string Key { get; set; }

    [Required(ErrorMessage = "'organizationId' must be provided")]
    public Guid OrganizationId { get; set; }
}
