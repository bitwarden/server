﻿using System.ComponentModel.DataAnnotations;

namespace Bit.Api.AdminConsole.Models.Request;

public class OrganizationDomainRequestModel
{
    [Required]
    public string Txt { get; set; }

    [Required]
    public string DomainName { get; set; }
}
