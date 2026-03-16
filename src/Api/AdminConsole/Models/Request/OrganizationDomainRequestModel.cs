// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.ComponentModel.DataAnnotations;
using Bit.Core.Utilities;

namespace Bit.Api.AdminConsole.Models.Request;

public class OrganizationDomainRequestModel
{
    [Required]
    [DomainNameValidator]
    public string DomainName { get; set; }
}
