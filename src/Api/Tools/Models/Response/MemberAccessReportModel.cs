using Bit.Core.Tools.Models.Data;

namespace Bit.Api.Tools.Models.Response;

/// <summary>
/// Contains the collections and group collections a user has access to including
/// the permission level for the collection and group collection.
/// </summary>
public class MemberAccessReportResponseModel
{
    public string UserName { get; set; }
    public string Email { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public bool AccountRecoveryEnabled { get; set; }
    public int GroupsCount { get; set; }
    public int CollectionsCount { get; set; }
    public int TotalItemCount { get; set; }
    public Guid? UserGuid { get; set; }
    public bool UsesKeyConnector { get; set; }
    public IEnumerable<MemberAccessDetails> AccessDetails { get; set; }

    public MemberAccessReportResponseModel(MemberAccessCipherDetails memberAccessCipherDetails)
    {
        this.UserName = memberAccessCipherDetails.UserName;
        this.Email = memberAccessCipherDetails.Email;
        this.TwoFactorEnabled = memberAccessCipherDetails.TwoFactorEnabled;
        this.AccountRecoveryEnabled = memberAccessCipherDetails.AccountRecoveryEnabled;
        this.GroupsCount = memberAccessCipherDetails.GroupsCount;
        this.CollectionsCount = memberAccessCipherDetails.CollectionsCount;
        this.TotalItemCount = memberAccessCipherDetails.TotalItemCount;
        this.UserGuid = memberAccessCipherDetails.UserGuid;
        this.AccessDetails = memberAccessCipherDetails.AccessDetails;
    }
}
