#nullable enable
using Bit.Core.Entities;
using Bit.Core.Models.Api;
using Bit.Core.SecretsManager.Entities;

namespace Bit.Api.SecretsManager.Models.Response;

public abstract class BaseAccessPolicyResponseModel : ResponseModel
{
    protected BaseAccessPolicyResponseModel(BaseAccessPolicy baseAccessPolicy, string obj) : base(obj)
    {
        Id = baseAccessPolicy.Id;
        Read = baseAccessPolicy.Read;
        Write = baseAccessPolicy.Write;
        CreationDate = baseAccessPolicy.CreationDate;
        RevisionDate = baseAccessPolicy.RevisionDate;
    }

    public Guid Id { get; set; }
    public bool Read { get; set; }
    public bool Write { get; set; }
    public DateTime CreationDate { get; set; }
    public DateTime RevisionDate { get; set; }

    public string? GetUserDisplayName(User? user)
    {
        return string.IsNullOrWhiteSpace(user?.Name) ? user?.Email : user?.Name;
    }
}

public class UserProjectAccessPolicyResponseModel : BaseAccessPolicyResponseModel
{
    private const string _objectName = "userProjectAccessPolicy";

    public UserProjectAccessPolicyResponseModel(UserProjectAccessPolicy accessPolicy, Guid currentUserId) : base(accessPolicy, _objectName)
    {
        CurrentUser = currentUserId == accessPolicy.User?.Id;
        SetProperties(accessPolicy);
    }

    public UserProjectAccessPolicyResponseModel() : base(new UserProjectAccessPolicy(), _objectName)
    {
    }

    public Guid? OrganizationUserId { get; set; }
    public string? OrganizationUserName { get; set; }
    public Guid? UserId { get; set; }
    public Guid? GrantedProjectId { get; set; }
    public bool? CurrentUser { get; set; }

    private void SetProperties(UserProjectAccessPolicy accessPolicy)
    {
        OrganizationUserId = accessPolicy.OrganizationUserId;
        GrantedProjectId = accessPolicy.GrantedProjectId;
        OrganizationUserName = GetUserDisplayName(accessPolicy.User);
        UserId = accessPolicy.User?.Id;
    }
}

public class UserServiceAccountAccessPolicyResponseModel : BaseAccessPolicyResponseModel
{
    private const string _objectName = "userServiceAccountAccessPolicy";

    public UserServiceAccountAccessPolicyResponseModel(UserServiceAccountAccessPolicy accessPolicy, Guid userId)
        : base(accessPolicy, _objectName)
    {
        SetProperties(accessPolicy);
        CurrentUser = accessPolicy.User?.Id == userId;
    }

    public UserServiceAccountAccessPolicyResponseModel() : base(new UserServiceAccountAccessPolicy(), _objectName)
    {
    }

    public Guid? OrganizationUserId { get; set; }
    public string? OrganizationUserName { get; set; }
    public Guid? UserId { get; set; }
    public Guid? GrantedServiceAccountId { get; set; }
    public bool CurrentUser { get; set; }

    private void SetProperties(UserServiceAccountAccessPolicy accessPolicy)
    {
        OrganizationUserId = accessPolicy.OrganizationUserId;
        GrantedServiceAccountId = accessPolicy.GrantedServiceAccountId;
        OrganizationUserName = GetUserDisplayName(accessPolicy.User);
        UserId = accessPolicy.User?.Id;
    }
}

public class UserSecretAccessPolicyResponseModel : BaseAccessPolicyResponseModel
{
    private const string _objectName = "userSecretAccessPolicy";

    public UserSecretAccessPolicyResponseModel(UserSecretAccessPolicy accessPolicy, Guid currentUserId) : base(accessPolicy, _objectName)
    {
        CurrentUser = currentUserId == accessPolicy.User?.Id;
        SetProperties(accessPolicy);
    }

    public UserSecretAccessPolicyResponseModel() : base(new UserSecretAccessPolicy(), _objectName)
    {
    }

    public Guid? OrganizationUserId { get; set; }
    public string? OrganizationUserName { get; set; }
    public Guid? UserId { get; set; }
    public Guid? GrantedSecretId { get; set; }
    public bool? CurrentUser { get; set; }

    private void SetProperties(UserSecretAccessPolicy accessPolicy)
    {
        OrganizationUserId = accessPolicy.OrganizationUserId;
        GrantedSecretId = accessPolicy.GrantedSecretId;
        OrganizationUserName = GetUserDisplayName(accessPolicy.User);
        UserId = accessPolicy.User?.Id;
    }
}

public class GroupProjectAccessPolicyResponseModel : BaseAccessPolicyResponseModel
{
    private const string _objectName = "groupProjectAccessPolicy";

    public GroupProjectAccessPolicyResponseModel(GroupProjectAccessPolicy accessPolicy)
        : base(accessPolicy, _objectName)
    {
        GroupId = accessPolicy.GroupId;
        GrantedProjectId = accessPolicy.GrantedProjectId;
        GroupName = accessPolicy.Group?.Name;
        CurrentUserInGroup = accessPolicy.CurrentUserInGroup;
    }

    public GroupProjectAccessPolicyResponseModel() : base(new GroupProjectAccessPolicy(), _objectName)
    {
    }

    public Guid? GroupId { get; set; }
    public string? GroupName { get; set; }
    public bool? CurrentUserInGroup { get; set; }
    public Guid? GrantedProjectId { get; set; }
}

public class GroupServiceAccountAccessPolicyResponseModel : BaseAccessPolicyResponseModel
{
    private const string _objectName = "groupServiceAccountAccessPolicy";

    public GroupServiceAccountAccessPolicyResponseModel(GroupServiceAccountAccessPolicy accessPolicy)
        : base(accessPolicy, _objectName)
    {
        GroupId = accessPolicy.GroupId;
        GroupName = accessPolicy.Group?.Name;
        GrantedServiceAccountId = accessPolicy.GrantedServiceAccountId;
        CurrentUserInGroup = accessPolicy.CurrentUserInGroup;
    }

    public GroupServiceAccountAccessPolicyResponseModel() : base(new GroupServiceAccountAccessPolicy(), _objectName)
    {
    }

    public Guid? GroupId { get; set; }
    public string? GroupName { get; set; }
    public Guid? GrantedServiceAccountId { get; set; }
    public bool? CurrentUserInGroup { get; set; }
}

public class GroupSecretAccessPolicyResponseModel : BaseAccessPolicyResponseModel
{
    private const string _objectName = "groupSecretAccessPolicy";

    public GroupSecretAccessPolicyResponseModel(GroupSecretAccessPolicy accessPolicy)
        : base(accessPolicy, _objectName)
    {
        GroupId = accessPolicy.GroupId;
        GrantedSecretId = accessPolicy.GrantedSecretId;
        GroupName = accessPolicy.Group?.Name;
        CurrentUserInGroup = accessPolicy.CurrentUserInGroup;
    }

    public GroupSecretAccessPolicyResponseModel() : base(new GroupSecretAccessPolicy(), _objectName)
    {
    }

    public Guid? GroupId { get; set; }
    public string? GroupName { get; set; }
    public bool? CurrentUserInGroup { get; set; }
    public Guid? GrantedSecretId { get; set; }
}

public class ServiceAccountProjectAccessPolicyResponseModel : BaseAccessPolicyResponseModel
{
    private const string _objectName = "serviceAccountProjectAccessPolicy";

    public ServiceAccountProjectAccessPolicyResponseModel(ServiceAccountProjectAccessPolicy accessPolicy)
        : base(accessPolicy, _objectName)
    {
        ServiceAccountId = accessPolicy.ServiceAccountId;
        GrantedProjectId = accessPolicy.GrantedProjectId;
        ServiceAccountName = accessPolicy.ServiceAccount?.Name;
        GrantedProjectName = accessPolicy.GrantedProject?.Name;
    }

    public ServiceAccountProjectAccessPolicyResponseModel()
        : base(new ServiceAccountProjectAccessPolicy(), _objectName)
    {
    }

    public Guid? ServiceAccountId { get; set; }
    public string? ServiceAccountName { get; set; }
    public Guid? GrantedProjectId { get; set; }
    public string? GrantedProjectName { get; set; }
}

public class ServiceAccountSecretAccessPolicyResponseModel : BaseAccessPolicyResponseModel
{
    private const string _objectName = "serviceAccountSecretAccessPolicy";

    public ServiceAccountSecretAccessPolicyResponseModel(ServiceAccountSecretAccessPolicy accessPolicy)
        : base(accessPolicy, _objectName)
    {
        ServiceAccountId = accessPolicy.ServiceAccountId;
        GrantedSecretId = accessPolicy.GrantedSecretId;
        ServiceAccountName = accessPolicy.ServiceAccount?.Name;
    }

    public ServiceAccountSecretAccessPolicyResponseModel()
        : base(new ServiceAccountSecretAccessPolicy(), _objectName)
    {
    }

    public Guid? ServiceAccountId { get; set; }
    public string? ServiceAccountName { get; set; }
    public Guid? GrantedSecretId { get; set; }
}
