#nullable enable
using Bit.Core.Models.Api;
using Bit.Core.SecretsManager.Models.Data;

namespace Bit.Api.SecretsManager.Models.Response;

public class ServiceAccountGrantedPoliciesPermissionDetailsResponseModel : ResponseModel
{
    private const string _objectName = "ServiceAccountGrantedPoliciesPermissionDetails";

    public ServiceAccountGrantedPoliciesPermissionDetailsResponseModel(
        ServiceAccountGrantedPoliciesPermissionDetails? grantedPoliciesPermissionDetails)
        : base(_objectName)
    {
        if (grantedPoliciesPermissionDetails == null)
        {
            return;
        }

        GrantedProjectPolicies = grantedPoliciesPermissionDetails.ProjectGrantedPolicies
            .Select(x => new GrantedProjectAccessPolicyPermissionDetailsResponseModel(x)).ToList();
    }

    public ServiceAccountGrantedPoliciesPermissionDetailsResponseModel() : base(_objectName)
    {
    }

    public List<GrantedProjectAccessPolicyPermissionDetailsResponseModel> GrantedProjectPolicies { get; set; } =
        [];
}
