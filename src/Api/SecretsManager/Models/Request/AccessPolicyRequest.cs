#nullable enable
using System.ComponentModel.DataAnnotations;
using Bit.Core.SecretsManager.Entities;

namespace Bit.Api.SecretsManager.Models.Request;

public class AccessPolicyRequest : IValidatableObject
{
    [Required]
    public Guid GranteeId { get; set; }

    [Required]
    public bool Read { get; set; }

    [Required]
    public bool Write { get; set; }

    [Required]
    public bool Manage { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Manage && !Write)
        {
            yield return new ValidationResult(
                "Write must be true when Manage is true.",
                [nameof(Write), nameof(Manage)]);
        }
    }

    public UserProjectAccessPolicy ToUserProjectAccessPolicy(Guid projectId, Guid organizationId) =>
        new()
        {
            OrganizationUserId = GranteeId,
            GrantedProjectId = projectId,
            GrantedProject = new Project { OrganizationId = organizationId, Id = projectId },
            Read = Read,
            Write = Write,
            Manage = Manage
        };

    public UserSecretAccessPolicy ToUserSecretAccessPolicy(Guid secretId, Guid organizationId) =>
        new()
        {
            OrganizationUserId = GranteeId,
            GrantedSecretId = secretId,
            GrantedSecret = new Secret { OrganizationId = organizationId, Id = secretId },
            Read = Read,
            Write = Write,
            Manage = Manage
        };

    public GroupProjectAccessPolicy ToGroupProjectAccessPolicy(Guid projectId, Guid organizationId) =>
        new()
        {
            GroupId = GranteeId,
            GrantedProjectId = projectId,
            GrantedProject = new Project { OrganizationId = organizationId, Id = projectId },
            Read = Read,
            Write = Write,
            Manage = Manage
        };

    public GroupSecretAccessPolicy ToGroupSecretAccessPolicy(Guid secretId, Guid organizationId) =>
        new()
        {
            GroupId = GranteeId,
            GrantedSecretId = secretId,
            GrantedSecret = new Secret { OrganizationId = organizationId, Id = secretId },
            Read = Read,
            Write = Write,
            Manage = Manage
        };

    public ServiceAccountProjectAccessPolicy ToServiceAccountProjectAccessPolicy(Guid projectId, Guid organizationId) =>
        new()
        {
            ServiceAccountId = GranteeId,
            GrantedProjectId = projectId,
            GrantedProject = new Project { OrganizationId = organizationId, Id = projectId },
            Read = Read,
            Write = Write,
            Manage = Manage
        };

    public ServiceAccountSecretAccessPolicy ToServiceAccountSecretAccessPolicy(Guid secretId, Guid organizationId) =>
        new()
        {
            ServiceAccountId = GranteeId,
            GrantedSecretId = secretId,
            GrantedSecret = new Secret { OrganizationId = organizationId, Id = secretId },
            Read = Read,
            Write = Write,
            Manage = Manage
        };

    public UserServiceAccountAccessPolicy ToUserServiceAccountAccessPolicy(Guid id, Guid organizationId) =>
        new()
        {
            OrganizationUserId = GranteeId,
            GrantedServiceAccountId = id,
            GrantedServiceAccount = new ServiceAccount() { OrganizationId = organizationId, Id = id },
            Read = Read,
            Write = Write,
            Manage = Manage
        };

    public GroupServiceAccountAccessPolicy ToGroupServiceAccountAccessPolicy(Guid id, Guid organizationId) =>
        new()
        {
            GroupId = GranteeId,
            GrantedServiceAccountId = id,
            GrantedServiceAccount = new ServiceAccount() { OrganizationId = organizationId, Id = id },
            Read = Read,
            Write = Write,
            Manage = Manage
        };
}
