using Bit.Core.Entities;
using Bit.Core.Models.Api;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.SecretsManager.Entities;

namespace Bit.Api.SecretsManager.Models.Response;

public class PotentialGranteeResponseModel : ResponseModel
{
    private const string _objectName = "potentialGrantee";

    public PotentialGranteeResponseModel(Group group)
        : base(_objectName)
    {
        if (group == null)
        {
            throw new ArgumentNullException(nameof(group));
        }

        Id = group.Id;
        Name = group.Name;
        Type = "group";
    }

    public PotentialGranteeResponseModel(OrganizationUserUserDetails user)
        : base(_objectName)
    {
        if (user == null)
        {
            throw new ArgumentNullException(nameof(user));
        }

        Id = user.Id;
        Name = user.Name;
        Email = user.Email;
        Type = "user";
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
}
