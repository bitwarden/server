namespace Bit.Admin.Models;

public abstract class PagedModel<T>
{
    public List<T> Items { get; set; }
    public int Page { get; set; }
    public int Count { get; set; }
    public int? PreviousPage => Page < 2 ? (int?)null : Page - 1;
    public int? NextPage => Items.Count < Count ? (int?)null : Page + 1;
}
