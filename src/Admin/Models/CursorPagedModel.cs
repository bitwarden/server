namespace Bit.Admin.Models;

public class CursorPagedModel<T>
{
    public List<T> Items { get; set; }
    public int Count { get; set; }
    public string Cursor { get; set; }
    public string NextCursor { get; set; }
}
