// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Scim.Utilities;

namespace Bit.Scim.Models;

public class ScimListResponseModel<T> : BaseScimModel
{
    public ScimListResponseModel()
        : base(ScimConstants.Scim2SchemaListResponse)
    { }

    public int TotalResults { get; set; }
    public int StartIndex { get; set; }
    public int ItemsPerPage { get; set; }
    public List<T> Resources { get; set; }
}
