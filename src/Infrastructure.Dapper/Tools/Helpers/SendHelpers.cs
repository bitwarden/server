using System.Data;
using Bit.Core.Tools.Entities;

namespace Bit.Infrastructure.Dapper.Tools.Helpers;

/// <summary>
/// Dapper helper methods for Sends
/// </summary>
public static class SendHelpers
{
    private static readonly DataTableBuilder<Send> _sendTableBuilder = new(
        [
            s => s.Id,
            s => s.UserId,
            s => s.OrganizationId,
            s => s.Type,
            s => s.Data,
            s => s.Key,
            s => s.Password,
            s => s.MaxAccessCount,
            s => s.AccessCount,
            s => s.CreationDate,
            s => s.RevisionDate,
            s => s.ExpirationDate,
            s => s.DeletionDate,
            s => s.Disabled,
            s => s.HideEmail,
        ]
    );

    /// <summary>
    /// Converts an IEnumerable of Sends to a DataTable
    /// </summary>
    /// <remarks>Contains a hardcoded list of properties and must be updated with model</remarks>
    /// <param name="sends">List of sends</param>
    /// <returns>A data table matching the schema of dbo.Send containing one row mapped from the items in <see cref="Send"/>s</returns>
    public static DataTable ToDataTable(this IEnumerable<Send> sends)
    {
        return _sendTableBuilder.Build(sends ?? []);
    }
}
