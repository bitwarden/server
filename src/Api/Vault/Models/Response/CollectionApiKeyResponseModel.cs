#nullable enable
using Bit.Core.Models.Api;
using Bit.Core.SecretsManager.Entities;

namespace Bit.Api.Vault.Models.Response;

/// <summary>
/// Response model for listing collection API keys. Does NOT include client secret.
/// </summary>
public class CollectionApiKeyResponseModel : ResponseModel
{
    private const string _objectName = "collectionApiKey";

    public CollectionApiKeyResponseModel(ApiKey apiKey) : base(_objectName)
    {
        Id = apiKey.Id;
        Name = apiKey.Name;
        OrganizationId = apiKey.OrganizationId;
        CollectionId = apiKey.CollectionId;
        ExpireAt = apiKey.ExpireAt;
        CreationDate = apiKey.CreationDate;
        RevisionDate = apiKey.RevisionDate;
    }

    public CollectionApiKeyResponseModel() : base(_objectName)
    {
    }

    public Guid Id { get; set; }
    public string? Name { get; set; }
    public Guid? OrganizationId { get; set; }
    public Guid? CollectionId { get; set; }
    public DateTime? ExpireAt { get; set; }
    public DateTime CreationDate { get; set; }
    public DateTime RevisionDate { get; set; }
}
