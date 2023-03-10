#nullable enable
using Bit.Core.Entities;
using Bit.Core.Utilities;

namespace Bit.Core.SecretsManager.Entities;

public abstract class BaseAccessPolicy
{
    public Guid Id { get; set; }

    // Access
    public bool Read { get; set; }
    public bool Write { get; set; }

    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public DateTime RevisionDate { get; set; } = DateTime.UtcNow;

    public void SetNewId()
    {
        Id = CoreHelpers.GenerateComb();
    }
}

public class UserProjectAccessPolicy : BaseAccessPolicy
{
    public Guid? OrganizationUserId { get; set; }
    public User? User { get; set; }
    public Guid? GrantedProjectId { get; set; }
    public Project? GrantedProject { get; set; }
}

public class UserServiceAccountAccessPolicy : BaseAccessPolicy
{
    public Guid? OrganizationUserId { get; set; }
    public User? User { get; set; }
    public Guid? GrantedServiceAccountId { get; set; }
    public ServiceAccount? GrantedServiceAccount { get; set; }
}

public class GroupProjectAccessPolicy : BaseAccessPolicy
{
    public Guid? GroupId { get; set; }
    public Group? Group { get; set; }
    public bool? CurrentUserInGroup { get; set; }
    public Guid? GrantedProjectId { get; set; }
    public Project? GrantedProject { get; set; }
}

public class GroupServiceAccountAccessPolicy : BaseAccessPolicy
{
    public Guid? GroupId { get; set; }
    public Group? Group { get; set; }
    public bool? CurrentUserInGroup { get; set; }
    public Guid? GrantedServiceAccountId { get; set; }
    public ServiceAccount? GrantedServiceAccount { get; set; }
}

public class ServiceAccountProjectAccessPolicy : BaseAccessPolicy
{
    public Guid? ServiceAccountId { get; set; }
    public ServiceAccount? ServiceAccount { get; set; }
    public Guid? GrantedProjectId { get; set; }
    public Project? GrantedProject { get; set; }
}
