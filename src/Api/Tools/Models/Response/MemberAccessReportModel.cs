namespace Bit.Api.Tools.Models.Response;

public class MemberAccessCollectionModel
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public int ItemCount { get; set; }
    public bool ReadOnly { get; set; }
    public bool HidePasswords { get; set; }
    public bool Manage { get; set; }

    public MemberAccessCollectionModel(
        Guid id,
        string name,
        int itemCount,
        bool readOnly,
        bool hidePasswords,
        bool manage
    )
    {
        Id = id;
        Name = name;
        ItemCount = itemCount;
        ReadOnly = readOnly;
        HidePasswords = hidePasswords;
        Manage = manage;
    }
}
public class MemberAccessGroupModel
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public IEnumerable<MemberAccessCollectionModel> Collections { get; set; }
}

public class MemberAccessReportAccessDetails
{
    public Guid CollectionId { get; set;}
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
    internal string Key 
    {  
        get 
        { 
            // If the group Id has a value. The key is the group. If not this is a user collection
            // the key needs to be included
            return GroupId.HasValue ? GroupId.ToString() : $"{CollectionId}|{UserGuid}";
        }
    }

    // public override bool Equals(object obj)
    // {
    //     return Key == (obj as MemberAccessReportAccessDetails).Key;
    // }

    // public override int GetHashCode()
    // {
    //     return Key.GetHashCode();
    // }

}
public class MemberAccessReportModel
{
    public string UserName { get; set; }
    public string Email { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public bool AccountRecoveryEnabled { get; set; }
    public int GroupCount { get; set; }
    public int CollectionsCount { get; set; }
    public int TotalItemCount { get; set; }
    public IEnumerable<MemberAccessReportAccessDetails> AccessDetails { get; set; }
    public IEnumerable<MemberAccessCollectionModel> Collections { get; set; }
    public IEnumerable<MemberAccessGroupModel> Groups { get; set; }
}
