using Bit.Core.Models.Api;
using Bit.Core.SecretsManager.Entities;

namespace Bit.Api.SecretsManager.Models.Response;

public class ServiceAccountPeopleAccessPoliciesResponseModel : ResponseModel
{
    private const string _objectName = "serviceAccountAccessPolicies";

    public ServiceAccountPeopleAccessPoliciesResponseModel(
        IEnumerable<BaseAccessPolicy> baseAccessPolicies,
        Guid userId
    )
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
                    UserAccessPolicies.Add(new UserAccessPolicyResponseModel(accessPolicy, userId));
                    break;
                case GroupServiceAccountAccessPolicy accessPolicy:
                    GroupAccessPolicies.Add(new GroupAccessPolicyResponseModel(accessPolicy));
                    break;
            }
        }
    }

    public ServiceAccountPeopleAccessPoliciesResponseModel()
        : base(_objectName) { }

    public List<UserAccessPolicyResponseModel> UserAccessPolicies { get; set; } = new();

    public List<GroupAccessPolicyResponseModel> GroupAccessPolicies { get; set; } = new();
}
