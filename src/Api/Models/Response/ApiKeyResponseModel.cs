using Bit.Core.Entities;
using Bit.Core.Models.Api;

namespace Bit.Api.Models.Response;

public class ApiKeyResponseModel : ResponseModel
{
    public ApiKeyResponseModel(OrganizationApiKey organizationApiKey, string obj = "apiKey")
        : base(obj)
    {
        ArgumentNullException.ThrowIfNull(organizationApiKey);
        ApiKey = organizationApiKey.ApiKey;
        RevisionDate = organizationApiKey.RevisionDate;
    }

    public ApiKeyResponseModel(User user, string obj = "apiKey")
        : base(obj)
    {
        ArgumentNullException.ThrowIfNull(user);
        ApiKey = user.ApiKey;
        RevisionDate = user.RevisionDate;
    }

    public string ApiKey { get; set; }
    public DateTime RevisionDate { get; set; }
}
