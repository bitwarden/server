namespace Bit.Core.Entities;

public interface ICollectionAccess
{
    Guid CollectionId { get; set; }
    bool ReadOnly { get; set; }
    bool HidePasswords { get; set; }
    bool Manage { get; set; }
}
