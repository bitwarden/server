using Bit.Core.Models.Api;
using Bit.Core.SecretsManager.Entities;

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

        Id = serviceAccount.Id;
        OrganizationId = serviceAccount.OrganizationId;
        Name = serviceAccount.Name;
        CreationDate = serviceAccount.CreationDate;
        RevisionDate = serviceAccount.RevisionDate;
    }

    public ServiceAccountResponseModel() : base(_objectName)
    {
    }

    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public string Name { get; set; }

    public DateTime CreationDate { get; set; }

    public DateTime RevisionDate { get; set; }
}
