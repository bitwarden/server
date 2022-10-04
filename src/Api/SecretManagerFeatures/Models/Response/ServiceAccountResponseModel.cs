using Bit.Core.Entities;
using Bit.Core.Models.Api;

namespace Bit.Api.SecretManagerFeatures.Models.Response;

public class ServiceAccountResponseModel : ResponseModel
{
    public ServiceAccountResponseModel(ServiceAccount serviceAccount, string obj = "serviceAccount")
        : base(obj)
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

    public string Id { get; set; }

    public string OrganizationId { get; set; }

    public string Name { get; set; }

    public DateTime CreationDate { get; set; }

    public DateTime RevisionDate { get; set; }
}

