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

        UserAccessPolicies = accessPolicies.UserAccessPolicies.Select(x => new UserSecretAccessPolicyResponseModel(x, userId)).ToList();
        GroupAccessPolicies = accessPolicies.GroupAccessPolicies.Select(x => new GroupSecretAccessPolicyResponseModel(x)).ToList();
        ServiceAccountAccessPolicies = accessPolicies.ServiceAccountAccessPolicies.Select(x => new ServiceAccountSecretAccessPolicyResponseModel(x)).ToList();
    }

    public SecretAccessPoliciesResponseModel() : base(_objectName)
    {
    }


    public List<UserSecretAccessPolicyResponseModel> UserAccessPolicies { get; set; } = [];
    public List<GroupSecretAccessPolicyResponseModel> GroupAccessPolicies { get; set; } = [];
    public List<ServiceAccountSecretAccessPolicyResponseModel> ServiceAccountAccessPolicies { get; set; } = [];

}
