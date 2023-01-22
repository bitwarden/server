using Bit.Core.Entities;
using Bit.Core.Models.Api;

namespace Bit.Api.SecretManagerFeatures.Models.Response;

public class SMImportResponseModel : ResponseModel
{
    public SMImportResponseModel(SMImport import, string obj = "SecretsManagerImportResponseModel") : base(obj)
    {
        Projects = import.Projects != null && import.Projects.Any() ? import.Projects.Select(p => new InnerProject
        {
            Id = p.Id,
            Name = p.Name,
        }) : null;

        Secrets = import.Secrets != null && import.Secrets.Any() ? import.Secrets.Select(s => new InnerSecret
        {
            Id = s.Id,
            Key = s.Key,
            Value = s.Value,
            Note = s.Note,
            ProjectIds = s.ProjectIds,
        }) : null;
    }

    public IEnumerable<InnerProject> Projects { get; set; }
    public IEnumerable<InnerSecret> Secrets { get; set; }

    public class InnerProject
    {
        public InnerProject() { }

        public InnerProject(Project project)
        {
            Id = project.Id;
            Name = project.Name;
            ImportErrorMessage = "";
        }

        public Guid Id { get; set; }
        public string Name { get; set; }
        public string ImportErrorMessage { get; set; }
    }

    public class InnerSecret
    {
        public InnerSecret() { }

        public InnerSecret(Secret secret)
        {
            Id = secret.Id;
            Key = secret.Key;
            Value = secret.Value;
            Note = secret.Note;
            ProjectIds = secret.Projects?.Select(p => p.Id);
            ImportErrorMessage = "";
        }

        public Guid Id { get; set; }
        public string Key { get; set; }
        public string Value { get; set; }
        public string Note { get; set; }
        public IEnumerable<Guid> ProjectIds { get; set; }
        public string ImportErrorMessage { get; set; }
    }
}
