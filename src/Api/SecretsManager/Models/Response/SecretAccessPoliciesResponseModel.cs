#nullable enable
using Bit.Core.Models.Api;
using Bit.Core.SecretsManager.Models.Data;

namespace Bit.Api.SecretsManager.Models.Response;

public class SecretAccessPoliciesResponseModel : ResponseModel
{
    private const string _objectName = "secretAccessPolicies";

    public SecretAccessPoliciesResponseModel(SecretAccessPolicies? accessPolicies, Guid userId) :
        base(_objectName)
    {
        if (accessPolicies == null)
        {
            return;
        }

        UserAccessPolicies = accessPolicies.UserAccessPolicies.Select(x => new UserAccessPolicyResponseModel(x, userId)).ToList();
        GroupAccessPolicies = accessPolicies.GroupAccessPolicies.Select(x => new GroupAccessPolicyResponseModel(x)).ToList();
        ServiceAccountAccessPolicies = accessPolicies.ServiceAccountAccessPolicies.Select(x => new ServiceAccountAccessPolicyResponseModel(x)).ToList();
    }

    public SecretAccessPoliciesResponseModel() : base(_objectName)
    {
    }


    public List<UserAccessPolicyResponseModel> UserAccessPolicies { get; set; } = [];
    public List<GroupAccessPolicyResponseModel> GroupAccessPolicies { get; set; } = [];
    public List<ServiceAccountAccessPolicyResponseModel> ServiceAccountAccessPolicies { get; set; } = [];

}
