using System.ComponentModel.DataAnnotations;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Core.Utilities;

namespace Bit.Api.AdminConsole.Models.Request.Providers;

public class ProviderOrganizationCreateRequestModel
{
    [Required]
    [StringLength(256)]
    [StrictEmailAddress]
    public string ClientOwnerEmail { get; set; }

    [Required]
    public OrganizationCreateRequestModel OrganizationCreateRequest { get; set; }
}
