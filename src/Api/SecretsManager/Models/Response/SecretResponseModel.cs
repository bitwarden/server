using Bit.Core.Entities;
using Bit.Core.Models.Api;

namespace Bit.Api.SecretsManager.Models.Response;

public class SecretResponseModel : ResponseModel
{
    private const string _objectName = "secret";

    public SecretResponseModel(Secret secret) : base(_objectName)
    {
        if (secret == null)
        {
            throw new ArgumentNullException(nameof(secret));
        }

        Id = secret.Id.ToString();
        OrganizationId = secret.OrganizationId.ToString();
        Key = secret.Key;
        Value = secret.Value;
        Note = secret.Note;
        CreationDate = secret.CreationDate;
        RevisionDate = secret.RevisionDate;
        Projects = secret.Projects?.Select(p => new InnerProject(p));
    }

    public SecretResponseModel() : base(_objectName)
    {
    }

    public string Id { get; set; }

    public string OrganizationId { get; set; }

    public string Key { get; set; }

    public string Value { get; set; }

    public string Note { get; set; }

    public DateTime CreationDate { get; set; }

    public DateTime RevisionDate { get; set; }

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
}
