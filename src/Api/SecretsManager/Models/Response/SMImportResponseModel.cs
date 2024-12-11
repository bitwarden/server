using Bit.Core.Models.Api;
using Bit.Core.SecretsManager.Commands.Porting;

namespace Bit.Api.SecretsManager.Models.Response;

public class SMImportResponseModel : ResponseModel
{
    public SMImportResponseModel(SMImport import, string obj = "SecretsManagerImportResponseModel")
        : base(obj)
    {
        Projects = import.Projects?.Select(p => new InnerProjectImportResponseModel(p));
        Secrets = import.Secrets?.Select(s => new InnerSecretImportResponseModel(s));
    }

    public IEnumerable<InnerProjectImportResponseModel> Projects { get; set; }
    public IEnumerable<InnerSecretImportResponseModel> Secrets { get; set; }

    public class InnerProjectImportResponseModel
    {
        public InnerProjectImportResponseModel() { }

        public InnerProjectImportResponseModel(SMImport.InnerProject project)
        {
            Id = project.Id;
            Name = project.Name;
        }

        public Guid Id { get; set; }
        public string Name { get; set; }
    }

    public class InnerSecretImportResponseModel
    {
        public InnerSecretImportResponseModel() { }

        public InnerSecretImportResponseModel(SMImport.InnerSecret secret)
        {
            Id = secret.Id;
            Key = secret.Key;
            Value = secret.Value;
            Note = secret.Note;
            ProjectIds = secret.ProjectIds;
        }

        public Guid Id { get; set; }
        public string Key { get; set; }
        public string Value { get; set; }
        public string Note { get; set; }
        public IEnumerable<Guid> ProjectIds { get; set; }
    }
}
