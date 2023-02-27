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

    public UserProjectAccessPolicyResponseModel(UserProjectAccessPolicy accessPolicy) : base(accessPolicy, _objectName)
    {
        OrganizationUserId = accessPolicy.OrganizationUserId;
        GrantedProjectId = accessPolicy.GrantedProjectId;
        OrganizationUserName = GetUserDisplayName(accessPolicy.User);
    }

    public UserProjectAccessPolicyResponseModel() : base(new UserProjectAccessPolicy(), _objectName)
    {
    }

    public Guid? OrganizationUserId { get; set; }
    public string? OrganizationUserName { get; set; }
    public Guid? GrantedProjectId { get; set; }
}

public class UserServiceAccountAccessPolicyResponseModel : BaseAccessPolicyResponseModel
{
    private const string _objectName = "userServiceAccountAccessPolicy";

    public UserServiceAccountAccessPolicyResponseModel(UserServiceAccountAccessPolicy accessPolicy)
        : base(accessPolicy, _objectName)
    {
        OrganizationUserId = accessPolicy.OrganizationUserId;
        GrantedServiceAccountId = accessPolicy.GrantedServiceAccountId;
        OrganizationUserName = GetUserDisplayName(accessPolicy.User);
    }

    public UserServiceAccountAccessPolicyResponseModel() : base(new UserServiceAccountAccessPolicy(), _objectName)
    {
    }

    public Guid? OrganizationUserId { get; set; }
    public string? OrganizationUserName { get; set; }
    public Guid? GrantedServiceAccountId { get; set; }
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
    }

    public GroupProjectAccessPolicyResponseModel() : base(new GroupProjectAccessPolicy(), _objectName)
    {
    }

    public Guid? GroupId { get; set; }
    public string? GroupName { get; set; }
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
    }

    public GroupServiceAccountAccessPolicyResponseModel() : base(new GroupServiceAccountAccessPolicy(), _objectName)
    {
    }

    public Guid? GroupId { get; set; }
    public string? GroupName { get; set; }
    public Guid? GrantedServiceAccountId { get; set; }
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
