// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

namespace Bit.Core.Models.Data;

public class CollectionAccessDetails
{
    public IEnumerable<CollectionAccessSelection> Groups { get; set; }
    public IEnumerable<CollectionAccessSelection> Users { get; set; }
}

