using Bit.Core.Entities;
using Bit.Core.Models.Api;

namespace Bit.Api.SecretManagerFeatures.Models.Response;

public class SMExportResponseModel : ResponseModel
{
    public SMExportResponseModel(IEnumerable<Project> projects, IEnumerable<Secret> secrets, string obj = "SecretsManagerExport") : base(obj)
    {
        Secrets = secrets.Select(s => new InnerSecret(s));
        Projects = projects.Select(p => new InnerProject(p));
    }

    public IEnumerable<InnerProject> Projects { get; set; }
    public IEnumerable<InnerSecret> Secrets { get; set; }

    public class InnerProject
    {
        public InnerProject(Project project)
        {
            Id = project.Id;
            Name = project.Name;
        }

        public Guid Id { get; set; }
        public string Name { get; set; }
    }

    public class InnerSecret
    {
        public InnerSecret(Secret secret)
        {
            Id = secret.Id.ToString();
            Key = secret.Key;
            Value = secret.Value;
            Note = secret.Note;
            ProjectIds = secret.Projects?.Select(p => p.Id);
        }

        public string Id { get; set; }
        public string Key { get; set; }
        public string Value { get; set; }
        public string Note { get; set; }
        public IEnumerable<Guid> ProjectIds { get; set; }
    }
}


