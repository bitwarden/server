#nullable enable
using Bit.Core.Entities;
using Bit.Core.Models.Api;
using Bit.Core.SecretsManager.Entities;

namespace Bit.Api.SecretsManager.Models.Response;

public abstract class BaseAccessPolicyResponseModel : ResponseModel
{
    protected BaseAccessPolicyResponseModel(BaseAccessPolicy baseAccessPolicy, string obj) : base(obj)
    {
        Read = baseAccessPolicy.Read;
        Write = baseAccessPolicy.Write;
    }

    public bool Read { get; set; }
    public bool Write { get; set; }

    protected static string? GetUserDisplayName(User? user)
    {
        return string.IsNullOrWhiteSpace(user?.Name) ? user?.Email : user?.Name;
    }
}

public class UserAccessPolicyResponseModel : BaseAccessPolicyResponseModel
{
    private const string _objectName = "userAccessPolicy";

    public UserAccessPolicyResponseModel(UserProjectAccessPolicy accessPolicy, Guid currentUserId) : base(accessPolicy, _objectName)
    {
        CurrentUser = currentUserId == accessPolicy.User?.Id;
        OrganizationUserId = accessPolicy.OrganizationUserId;
        OrganizationUserName = GetUserDisplayName(accessPolicy.User);
    }

    public UserAccessPolicyResponseModel(UserServiceAccountAccessPolicy accessPolicy, Guid currentUserId) : base(accessPolicy, _objectName)
    {
        CurrentUser = currentUserId == accessPolicy.User?.Id;
        OrganizationUserId = accessPolicy.OrganizationUserId;
        OrganizationUserName = GetUserDisplayName(accessPolicy.User);
    }

    public UserAccessPolicyResponseModel(UserSecretAccessPolicy accessPolicy, Guid currentUserId) : base(accessPolicy, _objectName)
    {
        CurrentUser = currentUserId == accessPolicy.User?.Id;
        OrganizationUserId = accessPolicy.OrganizationUserId;
        OrganizationUserName = GetUserDisplayName(accessPolicy.User);
    }

    public UserAccessPolicyResponseModel() : base(new UserProjectAccessPolicy(), _objectName)
    {
    }

    public Guid? OrganizationUserId { get; set; }
    public string? OrganizationUserName { get; set; }
    public bool? CurrentUser { get; set; }
}

public class GroupAccessPolicyResponseModel : BaseAccessPolicyResponseModel
{
    private const string _objectName = "groupAccessPolicy";

    public GroupAccessPolicyResponseModel(GroupProjectAccessPolicy accessPolicy)
        : base(accessPolicy, _objectName)
    {
        GroupId = accessPolicy.GroupId;
        GroupName = accessPolicy.Group?.Name;
        CurrentUserInGroup = accessPolicy.CurrentUserInGroup;
    }

    public GroupAccessPolicyResponseModel(GroupServiceAccountAccessPolicy accessPolicy)
        : base(accessPolicy, _objectName)
    {
        GroupId = accessPolicy.GroupId;
        GroupName = accessPolicy.Group?.Name;
        CurrentUserInGroup = accessPolicy.CurrentUserInGroup;
    }

    public GroupAccessPolicyResponseModel(GroupSecretAccessPolicy accessPolicy)
        : base(accessPolicy, _objectName)
    {
        GroupId = accessPolicy.GroupId;
        GroupName = accessPolicy.Group?.Name;
        CurrentUserInGroup = accessPolicy.CurrentUserInGroup;
    }

    public GroupAccessPolicyResponseModel() : base(new GroupProjectAccessPolicy(), _objectName)
    {
    }

    public Guid? GroupId { get; set; }
    public string? GroupName { get; set; }
    public bool? CurrentUserInGroup { get; set; }
}

public class ServiceAccountAccessPolicyResponseModel : BaseAccessPolicyResponseModel
{
    private const string _objectName = "serviceAccountProjectAccessPolicy";

    public ServiceAccountAccessPolicyResponseModel(ServiceAccountProjectAccessPolicy accessPolicy)
        : base(accessPolicy, _objectName)
    {
        ServiceAccountId = accessPolicy.ServiceAccountId;
        ServiceAccountName = accessPolicy.ServiceAccount?.Name;
    }

    public ServiceAccountAccessPolicyResponseModel(ServiceAccountSecretAccessPolicy accessPolicy)
        : base(accessPolicy, _objectName)
    {
        ServiceAccountId = accessPolicy.ServiceAccountId;
        ServiceAccountName = accessPolicy.ServiceAccount?.Name;
    }

    public ServiceAccountAccessPolicyResponseModel()
        : base(new ServiceAccountProjectAccessPolicy(), _objectName)
    {
    }

    public Guid? ServiceAccountId { get; set; }
    public string? ServiceAccountName { get; set; }
}

public class GrantedProjectAccessPolicyResponseModel : BaseAccessPolicyResponseModel
{
    private const string _objectName = "grantedProjectAccessPolicy";

    public GrantedProjectAccessPolicyResponseModel(ServiceAccountProjectAccessPolicy accessPolicy)
        : base(accessPolicy, _objectName)
    {
        GrantedProjectId = accessPolicy.GrantedProjectId;
        GrantedProjectName = accessPolicy.GrantedProject?.Name;
    }

    public GrantedProjectAccessPolicyResponseModel()
        : base(new ServiceAccountProjectAccessPolicy(), _objectName)
    {
    }

    public Guid? GrantedProjectId { get; set; }
    public string? GrantedProjectName { get; set; }
}
