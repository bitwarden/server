using Bit.Core.Entities;
using Bit.Core.Models.Api;

namespace Bit.Api.SecretsManager.Models.Response;

public class SecretWithProjectsListResponseModel : ResponseModel
{
    private const string _objectName = "SecretsWithProjectsList";

    public SecretWithProjectsListResponseModel(IEnumerable<Secret> secrets) : base(_objectName)
    {
        Secrets = secrets.Select(s => new InnerSecret(s));
        Projects = secrets.SelectMany(s => s.Projects).DistinctBy(p => p.Id).Select(p => new InnerProject(p));
    }

    public SecretWithProjectsListResponseModel() : base(_objectName)
    {
    }

    public IEnumerable<InnerSecret> Secrets { get; set; }
    public IEnumerable<InnerProject> Projects { get; set; }

    public class InnerProject
    {
        public InnerProject(Project project)
        {
            Id = project.Id;
            Name = project.Name;
        }

        public InnerProject()
        {
        }

        public Guid Id { get; set; }
        public string Name { get; set; }
    }

    public class InnerSecret
    {
        public InnerSecret(Secret secret)
        {
            Id = secret.Id.ToString();
            OrganizationId = secret.OrganizationId.ToString();
            Key = secret.Key;
            CreationDate = secret.CreationDate;
            RevisionDate = secret.RevisionDate;
            Projects = secret.Projects?.Select(p => new InnerProject(p));
        }

        public InnerSecret()
        {
        }

        public string Id { get; set; }

        public string OrganizationId { get; set; }

        public string Key { get; set; }

        public DateTime CreationDate { get; set; }

        public DateTime RevisionDate { get; set; }

        public IEnumerable<InnerProject> Projects { get; set; }
    }
}


