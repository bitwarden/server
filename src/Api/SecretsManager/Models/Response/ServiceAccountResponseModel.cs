using Bit.Core.Models.Api;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Models.Data;

namespace Bit.Api.SecretsManager.Models.Response;

public class ServiceAccountResponseModel : ResponseModel
{
    private const string _objectName = "serviceAccount";

    public ServiceAccountResponseModel(ServiceAccount serviceAccount) : base(_objectName)
    {
        if (serviceAccount == null)
        {
            throw new ArgumentNullException(nameof(serviceAccount));
        }

        Id = serviceAccount.Id.ToString();
        OrganizationId = serviceAccount.OrganizationId.ToString();
        Name = serviceAccount.Name;
        CreationDate = serviceAccount.CreationDate;
        RevisionDate = serviceAccount.RevisionDate;
    }

    public ServiceAccountResponseModel() : base(_objectName)
    {
    }

    public string Id { get; set; }

    public string OrganizationId { get; set; }

    public string Name { get; set; }

    public DateTime CreationDate { get; set; }

    public DateTime RevisionDate { get; set; }
}

public class ServiceAccountSecretsDetailsResponseModel : ServiceAccountResponseModel
{
    public ServiceAccountSecretsDetailsResponseModel(ServiceAccountSecretsDetails serviceAccountDetails) : base(serviceAccountDetails.ServiceAccount)
    {
        if (serviceAccountDetails == null)
        {
            throw new ArgumentNullException(nameof(serviceAccountDetails));
        }

        AccessToSecrets = serviceAccountDetails.AccessToSecrets;
    }

    public int AccessToSecrets { get; set; }
}
