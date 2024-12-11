namespace Bit.Core.Tools.Models.Data;

public class MemberAccessDetails
{
    public Guid? CollectionId { get; set; }
    public Guid? GroupId { get; set; }
    public string GroupName { get; set; }
    public string CollectionName { get; set; }
    public int ItemCount { get; set; }
    public bool? ReadOnly { get; set; }
    public bool? HidePasswords { get; set; }
    public bool? Manage { get; set; }

    /// <summary>
    /// The CipherIds associated with the group/collection access
    /// </summary>
    public IEnumerable<string> CollectionCipherIds { get; set; }
}

public class MemberAccessCipherDetails
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

    /// <summary>
    /// The details for the member's collection access depending
    /// on the collections and groups they are assigned to
    /// </summary>
    public IEnumerable<MemberAccessDetails> AccessDetails { get; set; }

    /// <summary>
    /// A distinct list of the cipher ids associated with
    /// the organization member
    /// </summary>
    public IEnumerable<string> CipherIds { get; set; }
}
