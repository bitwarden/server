// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Billing.Models.Requests;

public class UpdateClientOrganizationRequestBody
{
    [Required]
    [Range(0, int.MaxValue, ErrorMessage = "You cannot assign negative seats to a client organization.")]
    public int AssignedSeats { get; set; }

    [Required]
    public string Name { get; set; }
}
