using System.Data;
using Bit.Core.Vault.Entities;
using Dapper;

namespace Bit.Infrastructure.Dapper.Vault.Helpers;

public static class FolderHelpers
{
    public static DataTable ToDataTable(this IEnumerable<Folder> folders)
    {
        var foldersTable = new DataTable();
        foldersTable.SetTypeName("[dbo].[Folder]");

        var columnData = new List<(string name, Type type, Func<Folder, object> getter)>
        {
            (nameof(Folder.Id), typeof(Guid), c => c.Id),
            (nameof(Folder.UserId), typeof(Guid), c => c.UserId),
            (nameof(Folder.Name), typeof(string), c => c.Name),
            (nameof(Folder.CreationDate), typeof(DateTime), c => c.CreationDate),
            (nameof(Folder.RevisionDate), typeof(DateTime), c => c.RevisionDate),
        };

        return folders.BuildTable(foldersTable, columnData);
    }
}
