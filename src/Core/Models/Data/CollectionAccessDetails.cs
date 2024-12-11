namespace Bit.Core.Models.Data;

public class CollectionAccessDetails
{
    public IEnumerable<CollectionAccessSelection> Groups { get; set; }
    public IEnumerable<CollectionAccessSelection> Users { get; set; }
}
