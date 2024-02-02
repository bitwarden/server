using Bit.Core.Models.Api;
using Bit.Core.SecretsManager.Models.Data;

namespace Bit.Api.SecretsManager.Models.Response;

public class ServiceAccountProjectAccessPolicyPermissionDetailsResponseModel : ResponseModel
{
    private const string _objectName = "serviceAccountProjectAccessPolicyPermissionDetails";

    public ServiceAccountProjectAccessPolicyPermissionDetailsResponseModel(ServiceAccountProjectAccessPolicyPermissionDetails apPermissionDetails, string obj = _objectName) : base(obj)
    {
        AccessPolicy = new ServiceAccountProjectAccessPolicyResponseModel(apPermissionDetails.AccessPolicy);
        HasPermission = apPermissionDetails.HasPermission;
    }

    public ServiceAccountProjectAccessPolicyPermissionDetailsResponseModel()
        : base(_objectName)
    {
    }

    public ServiceAccountProjectAccessPolicyResponseModel AccessPolicy { get; set; }
    public bool HasPermission { get; set; }
}
