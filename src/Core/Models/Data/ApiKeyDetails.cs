using Bit.Core.Entities;

namespace Bit.Core.Models.Data;

public class ApiKeyDetails : ApiKey
{

}

public class ServiceAccountApiKeyDetails : ApiKeyDetails
{
    public ServiceAccountApiKeyDetails(ApiKey apiKey, Guid organizationId)
    {
        Id = apiKey.Id;
        ServiceAccountId = apiKey.ServiceAccountId;
        Name = apiKey.Name;
        ClientSecret = apiKey.ClientSecret;
        Scope = apiKey.Scope;
        EncryptedPayload = apiKey.EncryptedPayload;
        Key = apiKey.Key;
        ExpireAt = apiKey.ExpireAt;
        CreationDate = apiKey.CreationDate;
        RevisionDate = apiKey.RevisionDate;
        ServiceAccountOrganizationId = organizationId;
    }

    public Guid ServiceAccountOrganizationId { get; set; }
}
