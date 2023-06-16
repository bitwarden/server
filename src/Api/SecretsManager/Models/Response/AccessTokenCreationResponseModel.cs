#nullable enable
using Bit.Core.Models.Api;
using Bit.Core.SecretsManager.Entities;

namespace Bit.Api.SecretsManager.Models.Response;

public class AccessTokenCreationResponseModel : ResponseModel
{
    private const string _objectName = "accessTokenCreation";

    public AccessTokenCreationResponseModel(ApiKey apiKey) : base(_objectName)
    {
        Id = apiKey.Id;
        Name = apiKey.Name;
        ClientSecret = apiKey.ClientSecret;
        ExpireAt = apiKey.ExpireAt;
        CreationDate = apiKey.CreationDate;
        RevisionDate = apiKey.RevisionDate;
    }

    public AccessTokenCreationResponseModel() : base(_objectName)
    {
    }

    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? ClientSecret { get; set; }
    public DateTime? ExpireAt { get; set; }
    public DateTime CreationDate { get; set; }
    public DateTime RevisionDate { get; set; }
}
