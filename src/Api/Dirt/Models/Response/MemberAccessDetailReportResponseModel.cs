using Bit.Core.Dirt.Models.Data;
using Bit.Core.Enums;

namespace Bit.Api.Dirt.Models.Response;

public class MemberAccessDetailReportResponseModel
{
    public Guid? OrganizationUserId { get; set; }
    public Guid? UserId { get; set; }
    public string? UserName { get; set; }
    public string? Email { get; set; }
    public OrganizationUserStatusType Status { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public bool AccountRecoveryEnabled { get; set; }
    public bool UsesKeyConnector { get; set; }
    public Guid? CollectionId { get; set; }
    public Guid? GroupId { get; set; }
    public string? GroupName { get; set; }
    public string? CollectionName { get; set; }
    public bool? ReadOnly { get; set; }
    public bool? HidePasswords { get; set; }
    public bool? Manage { get; set; }
    public IEnumerable<Guid>? CipherIds { get; set; }

    public MemberAccessDetailReportResponseModel(MemberAccessReportDetail reportDetail)
    {
        OrganizationUserId = reportDetail.OrganizationUserId;
        UserId = reportDetail.UserId;
        UserName = reportDetail.UserName;
        Email = reportDetail.Email;
        Status = reportDetail.Status;
        TwoFactorEnabled = reportDetail.TwoFactorEnabled;
        AccountRecoveryEnabled = reportDetail.AccountRecoveryEnabled;
        UsesKeyConnector = reportDetail.UsesKeyConnector;
        CollectionId = reportDetail.CollectionId;
        GroupId = reportDetail.GroupId;
        GroupName = reportDetail.GroupName;
        CollectionName = reportDetail.CollectionName;
        ReadOnly = reportDetail.ReadOnly;
        HidePasswords = reportDetail.HidePasswords;
        Manage = reportDetail.Manage;
        CipherIds = reportDetail.CipherIds;
    }
}
