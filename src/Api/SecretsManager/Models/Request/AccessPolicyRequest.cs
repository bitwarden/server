#nullable enable
using System.ComponentModel.DataAnnotations;
using Bit.Core.SecretsManager.Entities;

namespace Bit.Api.SecretsManager.Models.Request;

public class AccessPolicyRequest
{
    [Required]
    public Guid GranteeId { get; set; }

    [Required]
    public bool Read { get; set; }

    [Required]
    public bool Write { get; set; }

    public UserProjectAccessPolicy ToUserProjectAccessPolicy(Guid projectId, Guid organizationId) =>
        new()
        {
            OrganizationUserId = GranteeId,
            GrantedProjectId = projectId,
            GrantedProject = new Project { OrganizationId = organizationId, Id = projectId },
            Read = Read,
            Write = Write
        };

    public UserSecretAccessPolicy ToUserSecretAccessPolicy(Guid secretId, Guid organizationId) =>
        new()
        {
            OrganizationUserId = GranteeId,
            GrantedSecretId = secretId,
            GrantedSecret = new Secret { OrganizationId = organizationId, Id = secretId },
            Read = Read,
            Write = Write
        };

    public GroupProjectAccessPolicy ToGroupProjectAccessPolicy(Guid projectId, Guid organizationId) =>
        new()
        {
            GroupId = GranteeId,
            GrantedProjectId = projectId,
            GrantedProject = new Project { OrganizationId = organizationId, Id = projectId },
            Read = Read,
            Write = Write
        };

    public GroupSecretAccessPolicy ToGroupSecretAccessPolicy(Guid secretId, Guid organizationId) =>
        new()
        {
            GroupId = GranteeId,
            GrantedSecretId = secretId,
            GrantedSecret = new Secret { OrganizationId = organizationId, Id = secretId },
            Read = Read,
            Write = Write
        };

    public ServiceAccountProjectAccessPolicy ToServiceAccountProjectAccessPolicy(Guid projectId, Guid organizationId) =>
        new()
        {
            ServiceAccountId = GranteeId,
            GrantedProjectId = projectId,
            GrantedProject = new Project { OrganizationId = organizationId, Id = projectId },
            Read = Read,
            Write = Write
        };

    public ServiceAccountSecretAccessPolicy ToServiceAccountSecretAccessPolicy(Guid secretId, Guid organizationId) =>
        new()
        {
            ServiceAccountId = GranteeId,
            GrantedSecretId = secretId,
            GrantedSecret = new Secret { OrganizationId = organizationId, Id = secretId },
            Read = Read,
            Write = Write
        };

    public UserServiceAccountAccessPolicy ToUserServiceAccountAccessPolicy(Guid id, Guid organizationId) =>
        new()
        {
            OrganizationUserId = GranteeId,
            GrantedServiceAccountId = id,
            GrantedServiceAccount = new ServiceAccount() { OrganizationId = organizationId, Id = id },
            Read = Read,
            Write = Write
        };

    public GroupServiceAccountAccessPolicy ToGroupServiceAccountAccessPolicy(Guid id, Guid organizationId) =>
        new()
        {
            GroupId = GranteeId,
            GrantedServiceAccountId = id,
            GrantedServiceAccount = new ServiceAccount() { OrganizationId = organizationId, Id = id },
            Read = Read,
            Write = Write
        };
}
