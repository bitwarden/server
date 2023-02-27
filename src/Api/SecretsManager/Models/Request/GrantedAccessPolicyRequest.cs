using System.ComponentModel.DataAnnotations;
using Bit.Core.SecretsManager.Entities;

namespace Bit.Api.SecretsManager.Models.Request;

public class GrantedAccessPolicyRequest
{
    [Required]
    public Guid GrantedId { get; set; }

    [Required]
    public bool Read { get; set; }

    [Required]
    public bool Write { get; set; }

    public ServiceAccountProjectAccessPolicy ToServiceAccountProjectAccessPolicy(Guid serviceAccountId) =>
        new()
        {
            ServiceAccountId = serviceAccountId,
            GrantedProjectId = GrantedId,
            Read = Read,
            Write = Write,
        };
}
