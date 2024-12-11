using Bit.Core.Models.Api;
using Bit.Core.SecretsManager.Entities;

namespace Bit.Api.SecretsManager.Models.Response;

public class SMExportResponseModel : ResponseModel
{
    public SMExportResponseModel(
        IEnumerable<Project> projects,
        IEnumerable<Secret> secrets,
        string obj = "SecretsManagerExportResponseModel"
    )
        : base(obj)
    {
        Secrets = secrets?.Select(s => new InnerSecretExportResponseModel(s));
        Projects = projects?.Select(p => new InnerProjectExportResponseModel(p));
    }

    public IEnumerable<InnerProjectExportResponseModel> Projects { get; set; }
    public IEnumerable<InnerSecretExportResponseModel> Secrets { get; set; }

    public class InnerProjectExportResponseModel
    {
        public InnerProjectExportResponseModel(Project project)
        {
            Id = project.Id;
            Name = project.Name;
        }

        public Guid Id { get; set; }
        public string Name { get; set; }
    }

    public class InnerSecretExportResponseModel
    {
        public InnerSecretExportResponseModel(Secret secret)
        {
            Id = secret.Id;
            Key = secret.Key;
            Value = secret.Value;
            Note = secret.Note;
            ProjectIds = secret.Projects?.Select(p => p.Id);
        }

        public Guid Id { get; set; }
        public string Key { get; set; }
        public string Value { get; set; }
        public string Note { get; set; }
        public IEnumerable<Guid> ProjectIds { get; set; }
    }
}
