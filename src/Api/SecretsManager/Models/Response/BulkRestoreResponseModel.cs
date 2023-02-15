#nullable enable
using Bit.Core.Models.Api;

namespace Bit.Api.SecretsManager.Models.Response;

public class BulkRestoreResponseModel : ResponseModel
{
    private const string _objectName = "BulkRestoreResponseModel";

    public BulkRestoreResponseModel(Guid id, string error) : base(_objectName)
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

    public BulkRestoreResponseModel() : base(_objectName)
    {
    }

    public Guid Id { get; set; }

    public string? Error { get; set; }
}
