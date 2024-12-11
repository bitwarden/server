using Bit.Scim.Utilities;

namespace Bit.Scim.Models;

public class ScimErrorResponseModel : BaseScimModel
{
    public ScimErrorResponseModel()
        : base(ScimConstants.Scim2SchemaError) { }

    public string Detail { get; set; }
    public int Status { get; set; }
}
