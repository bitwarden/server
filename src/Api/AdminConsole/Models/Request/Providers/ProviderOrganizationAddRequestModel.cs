// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.ComponentModel.DataAnnotations;

namespace Bit.Api.AdminConsole.Models.Request.Providers;

public class ProviderOrganizationAddRequestModel
{
    [Required]
    public Guid OrganizationId { get; set; }

    [Required]
    public string Key { get; set; }
}
