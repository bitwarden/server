using Bit.Core.Models.Api;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Models.Data;

namespace Bit.Api.SecretsManager.Models.Response;

public class SecretWithProjectsListResponseModel : ResponseModel
{
    private const string _objectName = "SecretsWithProjectsList";

    public SecretWithProjectsListResponseModel(IEnumerable<SecretPermissionDetails> secrets) : base(_objectName)
    {
        Secrets = secrets.Select(s => new InnerSecret(s));
        Projects = secrets.SelectMany(s => s.Secret.Projects).DistinctBy(p => p.Id).Select(p => new InnerProject(p));
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
        public InnerSecret(SecretPermissionDetails secret)
        {
            Id = secret.Secret.Id.ToString();
            OrganizationId = secret.Secret.OrganizationId.ToString();
            Key = secret.Secret.Key;
            CreationDate = secret.Secret.CreationDate;
            RevisionDate = secret.Secret.RevisionDate;
            Projects = secret.Secret.Projects?.Select(p => new InnerProject(p));
            Read = secret.Read;
            Write = secret.Write;
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
        public bool Read { get; set; }
        public bool Write { get; set; }
    }
}


