// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

namespace Bit.Core.Dirt.Reports.Models.Data;

public class OrganizationMemberBaseDetail
{
    public Guid? UserGuid { get; set; }
    public string UserName { get; set; }
    public string Email { get; set; }
    public string TwoFactorProviders { get; set; }
    public bool UsesKeyConnector { get; set; }
    public string ResetPasswordKey { get; set; }
    public Guid? CollectionId { get; set; }
    public Guid? GroupId { get; set; }
    public string GroupName { get; set; }
    public string CollectionName { get; set; }
    public bool? ReadOnly { get; set; }
    public bool? HidePasswords { get; set; }
    public bool? Manage { get; set; }
    public Guid CipherId { get; set; }
}
