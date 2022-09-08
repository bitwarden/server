namespace Bit.Core.Models.Data;

public class SelectionReadOnly
{
    public Guid Id { get; set; }
    public bool ReadOnly { get; set; }
    public bool HidePasswords { get; set; }
}
