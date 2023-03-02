using Bit.Core.Models.Api;
using Bit.Core.SecretsManager.Entities;

namespace Bit.Api.SecretsManager.Models.Response;

public class SecretWithProjectsListResponseModel : ResponseModel
{
    private const string _objectName = "SecretsWithProjectsList";

    public SecretWithProjectsListResponseModel(IEnumerable<Secret> secrets) : base(_objectName)
    {
        Secrets = secrets.Select(s => new SecretsWithProjectsInnerSecret(s));
        Projects = secrets.SelectMany(s => s.Projects).DistinctBy(p => p.Id).Select(p => new SecretWithProjectsInnerProject(p));
    }

    public SecretWithProjectsListResponseModel() : base(_objectName)
    {
    }

    public IEnumerable<SecretsWithProjectsInnerSecret> Secrets { get; set; }
    public IEnumerable<SecretWithProjectsInnerProject> Projects { get; set; }

    public class SecretWithProjectsInnerProject
    {
        public SecretWithProjectsInnerProject(Project project)
        {
            Id = project.Id;
            Name = project.Name;
        }

        public SecretWithProjectsInnerProject()
        {
        }

        public Guid Id { get; set; }
        public string Name { get; set; }
    }

    public class SecretsWithProjectsInnerSecret
    {
        public SecretsWithProjectsInnerSecret(Secret secret)
        {
            Id = secret.Id.ToString();
            OrganizationId = secret.OrganizationId.ToString();
            Key = secret.Key;
            CreationDate = secret.CreationDate;
            RevisionDate = secret.RevisionDate;
            Projects = secret.Projects?.Select(p => new SecretWithProjectsInnerProject(p));
        }

        public SecretsWithProjectsInnerSecret()
        {
        }

        public string Id { get; set; }

        public string OrganizationId { get; set; }

        public string Key { get; set; }

        public DateTime CreationDate { get; set; }

        public DateTime RevisionDate { get; set; }

        public IEnumerable<SecretWithProjectsInnerProject> Projects { get; set; }
    }
}


