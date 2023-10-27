using Bit.Core.Models.Api;
using Bit.Core.SecretsManager.Entities;

namespace Bit.Api.SecretsManager.Models.Response;

public class ProjectPeopleAccessPoliciesResponseModel : ResponseModel
{
    private const string _objectName = "projectPeopleAccessPolicies";

    public ProjectPeopleAccessPoliciesResponseModel(IEnumerable<BaseAccessPolicy> baseAccessPolicies, Guid userId)
        : base(_objectName)
    {
        foreach (var baseAccessPolicy in baseAccessPolicies)
        {
            switch (baseAccessPolicy)
            {
                case UserProjectAccessPolicy accessPolicy:
                    UserAccessPolicies.Add(new UserProjectAccessPolicyResponseModel(accessPolicy, userId));
                    break;
                case GroupProjectAccessPolicy accessPolicy:
                    GroupAccessPolicies.Add(new GroupProjectAccessPolicyResponseModel(accessPolicy));
                    break;
            }
        }
    }

    public ProjectPeopleAccessPoliciesResponseModel() : base(_objectName)
    {
    }

    public List<UserProjectAccessPolicyResponseModel> UserAccessPolicies { get; set; } = new();

    public List<GroupProjectAccessPolicyResponseModel> GroupAccessPolicies { get; set; } = new();
}
