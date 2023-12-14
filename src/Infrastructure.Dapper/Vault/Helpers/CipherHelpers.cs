using System.Data;
using Bit.Core.Vault.Entities;
using Dapper;

namespace Bit.Infrastructure.Dapper.Vault.Helpers;

public static class CipherHelpers
{
    public static DataTable ToDataTable(this IEnumerable<Cipher> ciphers)
    {
        var ciphersTable = new DataTable();
        ciphersTable.SetTypeName("[dbo].[Cipher]");

        var columnData = new List<(string name, Type type, Func<Cipher, object> getter)>
        {
            (nameof(Cipher.Id), typeof(Guid), c => c.Id),
            (nameof(Cipher.UserId), typeof(Guid), c => c.UserId),
            (nameof(Cipher.OrganizationId), typeof(Guid), c => c.OrganizationId),
            (nameof(Cipher.Type), typeof(short), c => c.Type),
            (nameof(Cipher.Data), typeof(string), c => c.Data),
            (nameof(Cipher.Favorites), typeof(string), c => c.Favorites),
            (nameof(Cipher.Folders), typeof(string), c => c.Folders),
            (nameof(Cipher.Attachments), typeof(string), c => c.Attachments),
            (nameof(Cipher.CreationDate), typeof(DateTime), c => c.CreationDate),
            (nameof(Cipher.RevisionDate), typeof(DateTime), c => c.RevisionDate),
            (nameof(Cipher.DeletedDate), typeof(DateTime), c => c.DeletedDate),
            (nameof(Cipher.Reprompt), typeof(short), c => c.Reprompt),
            (nameof(Cipher.Key), typeof(string), c => c.Key),
        };

        return ciphers.BuildTable(ciphersTable, columnData);
    }
}
