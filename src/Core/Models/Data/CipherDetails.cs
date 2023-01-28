namespace Core.Models.Data;

public class CipherDetails : CipherOrganizationDetails
{
    public Guid? FolderId { get; set; }
    public bool Favorite { get; set; }
    public bool Edit { get; set; }
    public bool ViewPassword { get; set; }
}
