#nullable enable
using Bit.Core.Models.Api;
using Bit.Core.SecretsManager.Models.Data;

namespace Bit.Api.SecretsManager.Models.Response;

public class GrantedProjectAccessPolicyPermissionDetailsResponseModel : ResponseModel
{
    private const string _objectName = "grantedProjectAccessPolicyPermissionDetails";

    public GrantedProjectAccessPolicyPermissionDetailsResponseModel(
        ServiceAccountProjectAccessPolicyPermissionDetails apPermissionDetails, string obj = _objectName) : base(obj)
    {
        AccessPolicy = new GrantedProjectAccessPolicyResponseModel(apPermissionDetails.AccessPolicy);
        HasPermission = apPermissionDetails.HasPermission;
    }

    public GrantedProjectAccessPolicyPermissionDetailsResponseModel()
        : base(_objectName)
    {
    }

    public GrantedProjectAccessPolicyResponseModel AccessPolicy { get; set; } = new();
    public bool HasPermission { get; set; }
}
