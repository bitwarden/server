using Bit.Core.Models.Api;
using Bit.Core.SecretsManager.Entities;

namespace Bit.Api.SecretsManager.Models.Response;

public class AccessTokenResponseModel : ResponseModel
{
    public AccessTokenResponseModel(ApiKey apiKey, string obj = "accessToken")
        : base(obj)
    {
        Id = apiKey.Id;
        Name = apiKey.Name;
        Scopes = apiKey.GetScopes();

        ExpireAt = apiKey.ExpireAt;
        CreationDate = apiKey.CreationDate;
        RevisionDate = apiKey.RevisionDate;
    }

    public Guid Id { get; set; }
    public string Name { get; set; }
    public ICollection<string> Scopes { get; set; }

    public DateTime? ExpireAt { get; set; }
    public DateTime CreationDate { get; set; }
    public DateTime RevisionDate { get; set; }
}
