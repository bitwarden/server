#nullable enable
using Bit.Core.Models.Api;
using Bit.Core.SecretsManager.Models.Data;

namespace Bit.Api.SecretsManager.Models.Response;

public class AccessTokenCreationResponseModel : ResponseModel
{
    private const string _objectName = "accessTokenCreation";

    public AccessTokenCreationResponseModel(ApiKeyClientSecretDetails details)
        : base(_objectName)
    {
        Id = details.ApiKey.Id;
        Name = details.ApiKey.Name;
        ExpireAt = details.ApiKey.ExpireAt;
        CreationDate = details.ApiKey.CreationDate;
        RevisionDate = details.ApiKey.RevisionDate;
        ClientSecret = details.ClientSecret;
    }

    public AccessTokenCreationResponseModel()
        : base(_objectName) { }

    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? ClientSecret { get; set; }
    public DateTime? ExpireAt { get; set; }
    public DateTime CreationDate { get; set; }
    public DateTime RevisionDate { get; set; }
}
