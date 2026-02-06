#nullable enable
using Bit.Core.Models.Api;

namespace Bit.Api.SecretsManager.Models.Response;

public class ServiceAccountCountsResponseModel() : ResponseModel(_objectName)
{
    private const string _objectName = "serviceAccountCounts";

    public int Projects { get; set; }

    public int People { get; set; }

    public int AccessTokens { get; set; }
}
