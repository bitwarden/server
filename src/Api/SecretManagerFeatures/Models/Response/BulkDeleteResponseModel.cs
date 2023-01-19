#nullable enable
using Bit.Core.Models.Api;

namespace Bit.Api.SecretManagerFeatures.Models.Response;

public class BulkDeleteResponseModel : ResponseModel
{
    public BulkDeleteResponseModel(Guid id, string error, string obj = "BulkDeleteResponseModel") : base(obj)
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

    public Guid Id { get; set; }

    public string? Error { get; set; }
}
