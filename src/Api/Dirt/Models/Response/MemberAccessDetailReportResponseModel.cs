using Bit.Core.Dirt.Reports.Models.Data;

namespace Bit.Api.Tools.Models.Response;

public class MemberAccessDetailReportResponseModel
{
    public Guid? UserGuid { get; set; }
    public string UserName { get; set; }
    public string Email { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public bool AccountRecoveryEnabled { get; set; }
    public bool UsesKeyConnector { get; set; }
    public Guid? CollectionId { get; set; }
    public Guid? GroupId { get; set; }
    public string GroupName { get; set; }
    public string CollectionName { get; set; }
    public bool? ReadOnly { get; set; }
    public bool? HidePasswords { get; set; }
    public bool? Manage { get; set; }
    public IEnumerable<Guid> CipherIds { get; set; }

    public MemberAccessDetailReportResponseModel(MemberAccessReportDetail reportDetail)
    {
        UserGuid = reportDetail.UserGuid;
        UserName = reportDetail.UserName;
        Email = reportDetail.Email;
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
