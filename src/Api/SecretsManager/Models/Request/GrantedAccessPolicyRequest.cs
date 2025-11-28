using System.ComponentModel.DataAnnotations;
using Bit.Infrastructure.EntityFramework.SecretsManager.Models;
using ServiceAccountProjectAccessPolicy = Bit.Core.SecretsManager.Entities.ServiceAccountProjectAccessPolicy;

namespace Bit.Api.SecretsManager.Models.Request;

public class GrantedAccessPolicyRequest
{
    [Required]
    public Guid GrantedId { get; set; }

    [Required]
    public bool Read { get; set; }

    [Required]
    public bool Write { get; set; }

    public ServiceAccountProjectAccessPolicy ToServiceAccountProjectAccessPolicy(Guid serviceAccountId, Guid organizationId) =>
        new()
        {
            ServiceAccountId = serviceAccountId,
            ServiceAccount = new ServiceAccount() { Id = serviceAccountId, OrganizationId = organizationId },
            GrantedProjectId = GrantedId,
            Read = Read,
            Write = Write,
        };
}
