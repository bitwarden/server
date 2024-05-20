#nullable enable
using Bit.Core.Models.Api;
using Bit.Core.SecretsManager.Models.Data;

namespace Bit.Api.SecretsManager.Models.Response;

public class ProjectServiceAccountsAccessPoliciesResponseModel : ResponseModel
{
    private const string _objectName = "ProjectServiceAccountsAccessPolicies";

    public ProjectServiceAccountsAccessPoliciesResponseModel(
        ProjectServiceAccountsAccessPolicies? projectServiceAccountsAccessPolicies)
        : base(_objectName)
    {
        if (projectServiceAccountsAccessPolicies == null)
        {
            return;
        }

        ServiceAccountAccessPolicies = projectServiceAccountsAccessPolicies.ServiceAccountAccessPolicies
            .Select(x => new ServiceAccountProjectAccessPolicyResponseModel(x)).ToList();
    }

    public ProjectServiceAccountsAccessPoliciesResponseModel() : base(_objectName)
    {
    }

    public List<ServiceAccountProjectAccessPolicyResponseModel> ServiceAccountAccessPolicies { get; set; } = [];
}
