using Bit.Core.Entities;
using Bit.Core.Models.Api;

namespace Bit.Api.SecretManagerFeatures.Models.Response;

public class ProjectResponseModel : ResponseModel
{
    public ProjectResponseModel(Project project, string obj = "project")
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
    }

    public string Id { get; set; }

    public string OrganizationId { get; set; }

    public string Name { get; set; }

    public DateTime CreationDate { get; set; }

    public DateTime RevisionDate { get; set; }

    public IEnumerable<Guid> Secrets { get; set; }
}
