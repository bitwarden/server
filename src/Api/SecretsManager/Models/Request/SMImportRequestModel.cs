using System.ComponentModel.DataAnnotations;
using Bit.Core.SecretsManager.Commands.Porting;
using Bit.Core.Utilities;

namespace Bit.Api.SecretsManager.Models.Request;

public class SMImportRequestModel
{
    public IEnumerable<InnerProjectImportRequestModel> Projects { get; set; }
    public IEnumerable<InnerSecretImportRequestModel> Secrets { get; set; }

    public class InnerProjectImportRequestModel
    {
        public InnerProjectImportRequestModel() { }

        [Required]
        public Guid Id { get; set; }

        [Required]
        [EncryptedString]
        [EncryptedStringLength(1000)]
        public string Name { get; set; }
    }

    public class InnerSecretImportRequestModel
    {
        public InnerSecretImportRequestModel() { }

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
