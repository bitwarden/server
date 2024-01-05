using System.Data;
using Bit.Core.Tools.Entities;

namespace Bit.Infrastructure.Dapper.Tools.Helpers;

/// <summary>
/// Dapper helper methods for Sends
/// </summary>
public static class SendHelpers
{
    /// <summary>
    /// Converts an IEnumerable of Sends to a DataTable
    /// </summary>
    /// <remarks>Contains a hardcoded list of properties and must be updated with model</remarks>
    /// <param name="sends">List of sends</param>
    /// <returns>A data table matching the schema of dbo.Send containing one row mapped from the items in <see cref="Send"/>s</returns>
    public static DataTable ToDataTable(this IEnumerable<Send> sends)
    {
        var sendsTable = new DataTable();

        var columnData = new List<(string name, Type type, Func<Send, object> getter)>
        {
            (nameof(Send.Id), typeof(Guid), c => c.Id),
            (nameof(Send.UserId), typeof(Guid), c => c.UserId),
            (nameof(Send.OrganizationId), typeof(Guid), c => c.OrganizationId),
            (nameof(Send.Type), typeof(short), c => c.Type),
            (nameof(Send.Data), typeof(string), c => c.Data),
            (nameof(Send.Key), typeof(string), c => c.Key),
            (nameof(Send.Password), typeof(string), c => c.Password),
            (nameof(Send.MaxAccessCount), typeof(int), c => c.MaxAccessCount),
            (nameof(Send.AccessCount), typeof(int), c => c.AccessCount),
            (nameof(Send.CreationDate), typeof(DateTime), c => c.CreationDate),
            (nameof(Send.RevisionDate), typeof(DateTime), c => c.RevisionDate),
            (nameof(Send.ExpirationDate), typeof(DateTime), c => c.ExpirationDate),
            (nameof(Send.DeletionDate), typeof(DateTime), c => c.DeletionDate),
            (nameof(Send.Disabled), typeof(bool), c => c.Disabled),
            (nameof(Send.HideEmail), typeof(bool), c => c.HideEmail),
        };

        return sends.BuildTable(sendsTable, columnData);
    }
}
