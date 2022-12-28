#nullable enable
using Bit.Core.Entities;
using Bit.Core.Models.Api;

namespace Bit.Api.SecretManagerFeatures.Models.Response;

public class ProjectAccessPoliciesResponseModel : ResponseModel
{
    public ProjectAccessPoliciesResponseModel(List<BaseAccessPolicy>? baseAccessPolicies,
        string obj = "projectAccessPolicies") : base(obj)
    {
        if (baseAccessPolicies == null) { return; }
        foreach (var baseAccessPolicy in baseAccessPolicies)
        {
            switch (baseAccessPolicy)
            {
                case UserProjectAccessPolicy accessPolicy:
                    UserAccessPolicies ??= new List<UserProjectAccessPolicyResponseModel>();
                    UserAccessPolicies.Add(new UserProjectAccessPolicyResponseModel(accessPolicy));
                    break;
                case GroupProjectAccessPolicy accessPolicy:
                    GroupAccessPolicies ??= new List<GroupProjectAccessPolicyResponseModel>();
                    GroupAccessPolicies.Add(new GroupProjectAccessPolicyResponseModel(accessPolicy));
                    break;
                case ServiceAccountProjectAccessPolicy accessPolicy:
                    ServiceAccountAccessPolicies ??= new List<ServiceAccountProjectAccessPolicyResponseModel>();
                    ServiceAccountAccessPolicies.Add(
                        new ServiceAccountProjectAccessPolicyResponseModel(accessPolicy));
                    break;
            }
        }
    }

    public List<UserProjectAccessPolicyResponseModel>? UserAccessPolicies { get; set; }
    public List<GroupProjectAccessPolicyResponseModel>? GroupAccessPolicies { get; set; }
    public List<ServiceAccountProjectAccessPolicyResponseModel>? ServiceAccountAccessPolicies { get; set; }
}
