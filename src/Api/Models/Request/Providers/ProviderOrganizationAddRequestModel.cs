using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Models.Request.Providers;

public class ProviderOrganizationAddRequestModel
{
    [Required]
    public Guid OrganizationId { get; set; }

    [Required]
    public string Key { get; set; }
}
