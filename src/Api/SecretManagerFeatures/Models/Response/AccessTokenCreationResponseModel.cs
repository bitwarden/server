#nullable enable
using Bit.Core.Entities;
using Bit.Core.Models.Api;

namespace Bit.Api.SecretManagerFeatures.Models.Response;

public class AccessTokenCreationResponseModel : ResponseModel
{
    public AccessTokenCreationResponseModel(ApiKey apiKey, string obj = "accessTokenCreation") : base(obj)
    {
        Id = apiKey.Id;
        Name = apiKey.Name;
        ClientSecret = apiKey.ClientSecret;
        ExpireAt = apiKey.ExpireAt;
        CreationDate = apiKey.CreationDate;
        RevisionDate = apiKey.RevisionDate;
    }

    public Guid Id { get; }
    public string Name { get; }
    public string ClientSecret { get; }
    public DateTime ExpireAt { get; }
    public DateTime CreationDate { get; }
    public DateTime RevisionDate { get; }
}
