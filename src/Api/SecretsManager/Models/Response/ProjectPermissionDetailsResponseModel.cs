using Bit.Core.SecretsManager.Entities;

namespace Bit.Api.SecretsManager.Models.Response;

public class ProjectPermissionDetailsResponseModel : ProjectResponseModel
{
    private const string _objectName = "projectPermissionDetails";

    public ProjectPermissionDetailsResponseModel(Project project, bool read, bool write, string obj = _objectName) : base(project, obj)
    {
        Read = read;
        Write = write;
    }

    public ProjectPermissionDetailsResponseModel()
    {
    }

    public bool Read { get; set; }

    public bool Write { get; set; }
}
