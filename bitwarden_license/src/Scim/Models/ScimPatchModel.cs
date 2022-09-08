using System.Text.Json;

namespace Bit.Scim.Models;

public class ScimPatchModel : BaseScimModel
{
    public ScimPatchModel()
        : base() { }

    public List<OperationModel> Operations { get; set; }

    public class OperationModel
    {
        public string Op { get; set; }
        public string Path { get; set; }
        public JsonElement Value { get; set; }
    }
}
