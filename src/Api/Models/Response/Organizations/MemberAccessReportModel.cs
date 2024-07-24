namespace Api.Models.Response.Organizations;

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
public class MemberAccessReportModel
{
    public string UserName { get; set; }
    public string Email { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public bool AccountRecoveryEnabled { get; set; }
    public int GroupCount { get; set; }
    public int CollectionsCount { get; set; }
    public int TotalItemCount { get; set; }
    public IEnumerable<MemberAccessCollectionModel> Collections { get; set; }
    public IEnumerable<MemberAccessGroupModel> Groups { get; set; }
}
