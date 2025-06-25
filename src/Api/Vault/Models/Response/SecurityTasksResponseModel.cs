using Bit.Core.Models.Api;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;

namespace Bit.Api.Vault.Models.Response;

public class SecurityTasksResponseModel : ResponseModel
{
    public SecurityTasksResponseModel(SecurityTask securityTask, string obj = "securityTask")
        : base(obj)
    {
        ArgumentNullException.ThrowIfNull(securityTask);

        Id = securityTask.Id;
        OrganizationId = securityTask.OrganizationId;
        CipherId = securityTask.CipherId;
        Type = securityTask.Type;
        Status = securityTask.Status;
        CreationDate = securityTask.CreationDate;
        RevisionDate = securityTask.RevisionDate;
    }

    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid? CipherId { get; set; }
    public SecurityTaskType Type { get; set; }
    public SecurityTaskStatus Status { get; set; }
    public DateTime CreationDate { get; set; }
    public DateTime RevisionDate { get; set; }
}
