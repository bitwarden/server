using System.ComponentModel.DataAnnotations;

namespace Bit.Api.SecretsManager.Models.Request;

public class BulkMoveToProjectsRequestModel
{
    [Required]
    public IReadOnlyList<Guid> Secrets { get; set; }

    [Required]
    public IReadOnlyList<Guid> Projects { get; set; }
}
