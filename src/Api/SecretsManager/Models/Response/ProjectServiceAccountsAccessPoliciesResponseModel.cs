using Bit.Core.Models.Api;
using Bit.Core.SecretsManager.Entities;

namespace Bit.Api.SecretsManager.Models.Response;

public class ProjectServiceAccountsAccessPoliciesResponseModel : ResponseModel
{
    private const string _objectName = "serviceAccountAccessPolicies";

    public ProjectServiceAccountsAccessPoliciesResponseModel(IEnumerable<BaseAccessPolicy> baseAccessPolicies)
        : base(_objectName)
    {
        if (baseAccessPolicies == null)
        {
            return;
        }

        foreach (var baseAccessPolicy in baseAccessPolicies)
        {
            switch (baseAccessPolicy)
            {
                case ServiceAccountProjectAccessPolicy accessPolicy:
                    ServiceAccountsAccessPolicies.Add(new ServiceAccountProjectAccessPolicyResponseModel(accessPolicy)); // TODO should I rename the response model? or rename everything else
                    break;
            }
        }
    }

    public ProjectServiceAccountsAccessPoliciesResponseModel() : base(_objectName)
    {
    }

    public List<ServiceAccountProjectAccessPolicyResponseModel> ServiceAccountsAccessPolicies { get; set; } = new();

}
