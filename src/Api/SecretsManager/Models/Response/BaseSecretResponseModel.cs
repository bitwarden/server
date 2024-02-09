#nullable enable

using System.ComponentModel.DataAnnotations;
using Bit.Core.Models.Api;
using Bit.Core.SecretsManager.Entities;

namespace Bit.Api.SecretsManager.Models.Response;

public class BaseSecretResponseModel : ResponseModel
{
    private const string _objectName = "baseSecret";

    public BaseSecretResponseModel(Secret secret, string objectName = _objectName) : base(objectName)
    {
        if (secret == null)
        {
            throw new ArgumentNullException(nameof(secret));
        }

        Id = secret.Id;
        OrganizationId = secret.OrganizationId;
        Key = secret.Key;
        Value = secret.Value;
        Note = secret.Note;
        CreationDate = secret.CreationDate;
        RevisionDate = secret.RevisionDate;
        Projects = secret.Projects?.Select(p => new SecretResponseInnerProject(p));
    }

    [Required]
    public Guid Id { get; }

    [Required]
    public Guid OrganizationId { get; }

    public string? Key { get; }

    public string? Value { get; }

    public string? Note { get; }

    [Required]
    public DateTime CreationDate { get; }

    [Required]
    public DateTime RevisionDate { get; }

    public IEnumerable<SecretResponseInnerProject>? Projects { get; init; }

    public class SecretResponseInnerProject
    {
        public SecretResponseInnerProject(Project project)
        {
            Id = project.Id;
            Name = project.Name;
        }

        public SecretResponseInnerProject()
        {
        }

        public Guid Id { get; set; }
        public string Name { get; set; }
    }
}
