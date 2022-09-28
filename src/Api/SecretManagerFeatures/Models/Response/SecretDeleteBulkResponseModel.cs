#nullable enable
using Bit.Core.Models.Api;

namespace Bit.Api.SecretManagerFeatures.Models.Response;

public class SecretDeleteBulkResponseModel : ResponseModel
{
    public SecretDeleteBulkResponseModel(Guid id, string error, string obj = "SecretDeleteBulkResponseModel") : base(obj)
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
