// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.Text.Json.Serialization;
using Bit.Scim.Utilities;

namespace Bit.Scim.Models;

public class ScimErrorResponseModel : BaseScimModel
{
    public ScimErrorResponseModel()
        : base(ScimConstants.Scim2SchemaError)
    { }

    /// <summary>
    /// A SCIM detail error keyword as defined in RFC 7644 §3.12 (e.g. "invalidFilter").
    /// Omitted from the response when not set.
    /// </summary>
    [JsonPropertyName("scimType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string ScimType { get; set; }

    public string Detail { get; set; }
    public int Status { get; set; }
}
