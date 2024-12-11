#nullable enable
using Bit.Core.Models.Api;

namespace Bit.Api.SecretsManager.Models.Response;

public class BulkDeleteResponseModel : ResponseModel
{
    private const string _objectName = "BulkDeleteResponseModel";

    public BulkDeleteResponseModel(Guid id, string error)
        : base(_objectName)
    {
        Id = id;

        if (string.IsNullOrWhiteSpace(error))
        {
            Error = null;
        }
        else
        {
            Error = error;
        }
    }

    public BulkDeleteResponseModel()
        : base(_objectName) { }

    public Guid Id { get; set; }

    public string? Error { get; set; }
}
