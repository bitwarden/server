using Bit.Core.Models.Api;
using Bit.Core.SecretsManager.Entities;

namespace Bit.Api.SecretsManager.Models.Response;

public class ServiceAccountPeopleAccessPoliciesResponseModel : ResponseModel
{
    private const string _objectName = "serviceAccountAccessPolicies";

    public ServiceAccountPeopleAccessPoliciesResponseModel(IEnumerable<BaseAccessPolicy> baseAccessPolicies)
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
                case UserServiceAccountAccessPolicy accessPolicy:
                    UserAccessPolicies.Add(new UserServiceAccountAccessPolicyResponseModel(accessPolicy));
                    break;
                case GroupServiceAccountAccessPolicy accessPolicy:
                    GroupAccessPolicies.Add(new GroupServiceAccountAccessPolicyResponseModel(accessPolicy));
                    break;
            }
        }
    }

    public ServiceAccountPeopleAccessPoliciesResponseModel() : base(_objectName)
    {
    }

    public List<UserServiceAccountAccessPolicyResponseModel> UserAccessPolicies { get; set; } = new();

    public List<GroupServiceAccountAccessPolicyResponseModel> GroupAccessPolicies { get; set; } = new();
}
