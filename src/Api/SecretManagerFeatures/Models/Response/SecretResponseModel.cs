using Bit.Core.Entities;
using Bit.Core.Models.Api;

namespace Bit.Api.SecretManagerFeatures.Models.Response;

public class SecretResponseModel : ResponseModel
{
    public SecretResponseModel(Secret secret, string obj = "secret")
        : base(obj)
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

    public string Id { get; set; }

    public string OrganizationId { get; set; }

    public string Key { get; set; }

    public string Value { get; set; }

    public string Note { get; set; }

    public DateTime CreationDate { get; set; }

    public DateTime RevisionDate { get; set; }

    public IEnumerable<InnerProject>? Projects { get; set; }

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
}
