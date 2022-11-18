using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities;
using Bit.Core.Utilities;

namespace Bit.Api.SecretManagerFeatures.Models.Request;

public class SecretUpdateRequestModel
{
    [Required]
    [EncryptedString]
    public string Key { get; set; }

    [Required]
    [EncryptedString]
    public string Value { get; set; }

    [Required]
    [EncryptedString]
    public string Note { get; set; }

    public Guid[] ProjectIds { get; set; }

    public Secret ToSecret(Guid id)
    {
        List<Project> assignedProjects = new List<Project>();

        foreach(Guid projectId in ProjectIds){
            var project = new Project();
            project.Id = projectId;
            assignedProjects.Add(project);
        }

        return new Secret()
        {
            Id = id,
            Key = this.Key,
            Value = this.Value,
            Note = this.Note,
            DeletedDate = null,
            Projects = assignedProjects
        };
    }
}
