﻿// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Api.Models.Request.Accounts;

namespace Bit.Api.Models.Request;

public class ExpandedTaxInfoUpdateRequestModel : TaxInfoUpdateRequestModel
{
    public string TaxId { get; set; }
    public string Line1 { get; set; }
    public string Line2 { get; set; }
    public string City { get; set; }
    public string State { get; set; }
}
