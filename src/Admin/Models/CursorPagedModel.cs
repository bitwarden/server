// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

namespace Bit.Admin.Models;

public class CursorPagedModel<T>
{
    public List<T> Items { get; set; }
    public int Count { get; set; }
    public string Cursor { get; set; }
    public string NextCursor { get; set; }
}
