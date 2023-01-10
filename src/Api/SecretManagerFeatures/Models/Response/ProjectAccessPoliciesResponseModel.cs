using Bit.Core.Entities;
using Bit.Core.Models.Api;

namespace Bit.Api.SecretManagerFeatures.Models.Response;

public class ProjectAccessPoliciesResponseModel : ResponseModel
{
    private const string _objectName = "projectAccessPolicies";

    public ProjectAccessPoliciesResponseModel(IEnumerable<BaseAccessPolicy>? baseAccessPolicies)
        : base(_objectName)
    {
        if (baseAccessPolicies == null) return;
        foreach (var baseAccessPolicy in baseAccessPolicies)
            switch (baseAccessPolicy)
            {
                case UserProjectAccessPolicy accessPolicy:
                    UserAccessPolicies.Add(new UserProjectAccessPolicyResponseModel(accessPolicy));
                    break;
                case GroupProjectAccessPolicy accessPolicy:
                    GroupAccessPolicies.Add(new GroupProjectAccessPolicyResponseModel(accessPolicy));
                    break;
                case ServiceAccountProjectAccessPolicy accessPolicy:
                    ServiceAccountAccessPolicies.Add(
                        new ServiceAccountProjectAccessPolicyResponseModel(accessPolicy));
                    break;
            }
    }

    public ProjectAccessPoliciesResponseModel() : base(_objectName)
    {
    }

    public List<UserProjectAccessPolicyResponseModel> UserAccessPolicies { get; set; } = new();

    public List<GroupProjectAccessPolicyResponseModel> GroupAccessPolicies { get; set; } = new();

    public List<ServiceAccountProjectAccessPolicyResponseModel> ServiceAccountAccessPolicies { get; set; } = new();
}
