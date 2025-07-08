// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.ComponentModel.DataAnnotations;

namespace Bit.Api.AdminConsole.Models.Request;

public class OrganizationDomainRequestModel
{
    [Required]
    public string DomainName { get; set; }
}
