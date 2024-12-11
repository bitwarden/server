using Bit.Core.Models.Api;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Models.Data;

namespace Bit.Api.SecretsManager.Models.Response;

public class SecretWithProjectsListResponseModel : ResponseModel
{
    private const string _objectName = "SecretsWithProjectsList";

    public SecretWithProjectsListResponseModel(IEnumerable<SecretPermissionDetails> secrets)
        : base(_objectName)
    {
        Secrets = secrets.Select(s => new SecretsWithProjectsInnerSecret(s));
        Projects = secrets
            .SelectMany(s => s.Secret.Projects)
            .DistinctBy(p => p.Id)
            .Select(p => new SecretWithProjectsInnerProject(p));
    }

    public SecretWithProjectsListResponseModel()
        : base(_objectName) { }

    public IEnumerable<SecretsWithProjectsInnerSecret> Secrets { get; set; }
    public IEnumerable<SecretWithProjectsInnerProject> Projects { get; set; }

    public class SecretWithProjectsInnerProject
    {
        public SecretWithProjectsInnerProject(Project project)
        {
            Id = project.Id;
            Name = project.Name;
        }

        public SecretWithProjectsInnerProject() { }

        public Guid Id { get; set; }
        public string Name { get; set; }
    }

    public class SecretsWithProjectsInnerSecret
    {
        public SecretsWithProjectsInnerSecret(SecretPermissionDetails secret)
        {
            Id = secret.Secret.Id;
            OrganizationId = secret.Secret.OrganizationId;
            Key = secret.Secret.Key;
            CreationDate = secret.Secret.CreationDate;
            RevisionDate = secret.Secret.RevisionDate;
            Projects = secret.Secret.Projects?.Select(p => new SecretWithProjectsInnerProject(p));
            Read = secret.Read;
            Write = secret.Write;
        }

        public SecretsWithProjectsInnerSecret() { }

        public Guid Id { get; set; }

        public Guid OrganizationId { get; set; }

        public string Key { get; set; }

        public DateTime CreationDate { get; set; }

        public DateTime RevisionDate { get; set; }

        public IEnumerable<SecretWithProjectsInnerProject> Projects { get; set; }
        public bool Read { get; set; }
        public bool Write { get; set; }
    }
}
