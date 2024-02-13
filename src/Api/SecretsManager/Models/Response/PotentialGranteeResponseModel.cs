using Bit.Core.Models.Api;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Models.Data;

namespace Bit.Api.SecretsManager.Models.Response;

public class PotentialGranteeResponseModel : ResponseModel
{
    private const string _objectName = "potentialGrantee";

    public PotentialGranteeResponseModel(GroupGrantee grantee)
        : base(_objectName)
    {
        if (grantee == null)
        {
            throw new ArgumentNullException(nameof(grantee));
        }

        Type = "group";
        Id = grantee.GroupId;
        Name = grantee.Name;
        CurrentUserInGroup = grantee.CurrentUserInGroup;
    }

    public PotentialGranteeResponseModel(UserGrantee grantee)
        : base(_objectName)
    {
        if (grantee == null)
        {
            throw new ArgumentNullException(nameof(grantee));
        }

        Type = "user";
        Id = grantee.OrganizationUserId;
        Name = grantee.Name;
        Email = grantee.Email;
        CurrentUser = grantee.CurrentUser;
    }

    public PotentialGranteeResponseModel(ServiceAccount serviceAccount)
        : base(_objectName)
    {
        if (serviceAccount == null)
        {
            throw new ArgumentNullException(nameof(serviceAccount));
        }

        Id = serviceAccount.Id;
        Name = serviceAccount.Name;
        Type = "serviceAccount";
    }

    public PotentialGranteeResponseModel(Project project)
        : base(_objectName)
    {
        if (project == null)
        {
            throw new ArgumentNullException(nameof(project));
        }

        Id = project.Id;
        Name = project.Name;
        Type = "project";
    }

    public PotentialGranteeResponseModel() : base(_objectName)
    {
    }

    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Type { get; set; }
    public string Email { get; set; }
    public bool CurrentUserInGroup { get; set; }
    public bool CurrentUser { get; set; }
}
