// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Scim.Utilities;

namespace Bit.Scim.Models;

public class ScimErrorResponseModel : BaseScimModel
{
    public ScimErrorResponseModel()
        : base(ScimConstants.Scim2SchemaError)
    { }

    public string Detail { get; set; }
    public int Status { get; set; }
}
