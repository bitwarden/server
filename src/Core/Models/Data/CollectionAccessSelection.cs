namespace Bit.Core.Models.Data;

public class CollectionAccessSelection
{
    public Guid Id { get; set; }
    public bool ReadOnly { get; set; }
    public bool HidePasswords { get; set; }
    public bool Manage { get; set; }
}
