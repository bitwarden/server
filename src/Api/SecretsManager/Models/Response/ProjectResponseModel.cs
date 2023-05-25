using Bit.Core.Models.Api;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Models.Data;

namespace Bit.Api.SecretsManager.Models.Response;

public class ProjectResponseModel : ResponseModel
{
    private const string _objectName = "project";

    public ProjectResponseModel(Project project, bool read, bool write, string obj = _objectName)
        : base(obj)
    {
        if (project == null)
        {
            throw new ArgumentNullException(nameof(project));
        }

        Id = project.Id.ToString();
        OrganizationId = project.OrganizationId.ToString();
        Name = project.Name;
        CreationDate = project.CreationDate;
        RevisionDate = project.RevisionDate;
        Read = read;
        Write = write;
    }

    public ProjectResponseModel(ProjectPermissionDetails projectDetails, string obj = _objectName)
        : base(obj)
    {
        if (projectDetails == null)
        {
            throw new ArgumentNullException(nameof(projectDetails));
        }

        Id = projectDetails.Project.Id.ToString();
        OrganizationId = projectDetails.Project.OrganizationId.ToString();
        Name = projectDetails.Project.Name;
        CreationDate = projectDetails.Project.CreationDate;
        RevisionDate = projectDetails.Project.RevisionDate;
        Read = projectDetails.Read;
        Write = projectDetails.Write;
    }

    public ProjectResponseModel() : base(_objectName)
    {
    }

    public string Id { get; set; }

    public string OrganizationId { get; set; }

    public string Name { get; set; }

    public DateTime CreationDate { get; set; }

    public DateTime RevisionDate { get; set; }

    public bool Read { get; set; }

    public bool Write { get; set; }
}
