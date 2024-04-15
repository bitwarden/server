#nullable enable
using Bit.Core.Models.Api;
using Bit.Core.SecretsManager.Models.Data;

namespace Bit.Api.SecretsManager.Models.Response;

public class ProjectServiceAccountsPoliciesPermissionDetailsResponseModel : ResponseModel
{
    private const string _objectName = "ProjectServiceAccountsPoliciesPermissionDetails";

    public ProjectServiceAccountsPoliciesPermissionDetailsResponseModel(
        ProjectServiceAccountsPoliciesPermissionDetails? policyPermissionDetails)
        : base(_objectName)
    {
        if (policyPermissionDetails == null)
        {
            return;
        }

        ServiceAccountPolicies = policyPermissionDetails.ServiceAccountPoliciesDetails
            .Select(x => new ServiceAccountProjectAccessPolicyPermissionDetailsResponseModel(x)).ToList();
    }

    public ProjectServiceAccountsPoliciesPermissionDetailsResponseModel() : base(_objectName)
    {
    }

    public List<ServiceAccountProjectAccessPolicyPermissionDetailsResponseModel> ServiceAccountPolicies { get; set; } =
        [];
}
