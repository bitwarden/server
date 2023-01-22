using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities;
using Bit.Core.Utilities;

namespace Bit.Api.SecretManagerFeatures.Models.Request;

public class SMImportRequestModel
{
    public IEnumerable<InnerProject> Projects { get; set; }
    public IEnumerable<InnerSecret> Secrets { get; set; }

    public class InnerProject
    {
        public InnerProject(Project project)
        {
            Id = project.Id;
            Name = project.Name;
        }

        [Required]
        public Guid Id { get; set; }

        [Required]
        [EncryptedString]
        [EncryptedStringLength(1000)]
        public string Name { get; set; }
    }

    public class InnerSecret
    {
        public InnerSecret(Secret secret)
        {
            Id = secret.Id;
            Key = secret.Key;
            Value = secret.Value;
            Note = secret.Note;
            ProjectIds = secret.Projects?.Select(p => p.Id);
        }

        [Required]
        public Guid Id { get; set; }

        [Required]
        [EncryptedString]
        [EncryptedStringLength(1000)]
        public string Key { get; set; }

        [Required]
        [EncryptedString]
        [EncryptedStringLength(1000)]
        public string Value { get; set; }

        [Required]
        [EncryptedString]
        [EncryptedStringLength(1000)]
        public string Note { get; set; }

        [Required]
        public IEnumerable<Guid> ProjectIds { get; set; }
    }

    public SMImport ToSMImport()
    {
        return new SMImport
        {
            Projects = Projects?.Select(p => new SMImport.InnerProject
            {
                Id = p.Id,
                Name = p.Name,
            }),
            Secrets = Secrets?.Select(s => new SMImport.InnerSecret
            {
                Id = s.Id,
                Key = s.Key,
                Value = s.Value,
                Note = s.Note,
                ProjectIds = s.ProjectIds,
            }),
        };
    }
}
