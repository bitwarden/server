#nullable enable
using Bit.Core.Models.Api;
using Bit.Core.SecretsManager.Models.Data;

namespace Bit.Api.Vault.Models.Response;

public class CollectionApiKeyCreationResponseModel : ResponseModel
{
    private const string _objectName = "collectionApiKeyCreation";

    public CollectionApiKeyCreationResponseModel(ApiKeyClientSecretDetails details) : base(_objectName)
    {
        Id = details.ApiKey.Id;
        Name = details.ApiKey.Name;
        OrganizationId = details.ApiKey.OrganizationId;
        CollectionId = details.ApiKey.CollectionId;
        ExpireAt = details.ApiKey.ExpireAt;
        CreationDate = details.ApiKey.CreationDate;
        RevisionDate = details.ApiKey.RevisionDate;
        ClientSecret = details.ClientSecret;
    }

    public CollectionApiKeyCreationResponseModel() : base(_objectName)
    {
    }

    public Guid Id { get; set; }
    public string? Name { get; set; }
    public Guid? OrganizationId { get; set; }
    public Guid? CollectionId { get; set; }
    public string? ClientSecret { get; set; }
    public DateTime? ExpireAt { get; set; }
    public DateTime CreationDate { get; set; }
    public DateTime RevisionDate { get; set; }
}
