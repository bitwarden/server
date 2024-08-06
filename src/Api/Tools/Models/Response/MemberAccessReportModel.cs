namespace Bit.Api.Tools.Models.Response;

public class MemberAccessReportAccessDetails
{
    public Guid CollectionId { get; set; }
    public Guid? GroupId { get; set; }
    public string GroupName { get; set; }
    public string CollectionName { get; set; }
    public int ItemCount { get; set; }
    public bool ReadOnly { get; set; }
    public bool HidePasswords { get; set; }
    public bool Manage { get; set; }

    // internal to not expose 
    internal ICollection<Guid> CipherIds { get; set; }
    internal Guid? UserGuid { get; set; }
}
public class MemberAccessReportModel
{
    public string UserName { get; set; }
    public string Email { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public bool AccountRecoveryEnabled { get; set; }
    public int GroupsCount { get; set; }
    public int CollectionsCount { get; set; }
    public int TotalItemCount { get; set; }
    public IEnumerable<MemberAccessReportAccessDetails> AccessDetails { get; set; }

}
