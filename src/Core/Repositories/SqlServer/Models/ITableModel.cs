namespace Bit.Core.Repositories.SqlServer.Models
{
    public interface ITableModel<T>
    {
        T ToDomain();
    }
}
