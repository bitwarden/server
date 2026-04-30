using System.Data;
using Bit.Core.Tools.Entities;

namespace Bit.Infrastructure.Dapper.Tools.Helpers;

/// <summary>
/// Dapper helper methods for Receives
/// </summary>
public static class ReceiveHelpers
{
    private static readonly DataTableBuilder<Receive> _receiveTableBuilder = new(
        [
            r => r.Id,
            r => r.UserId,
            r => r.Data,
            r => r.UserKeyWrappedSharedContentEncryptionKey,
            r => r.UserKeyWrappedPrivateKey,
            r => r.ScekWrappedPublicKey,
            r => r.Secret,
            r => r.UploadCount,
            r => r.CreationDate,
            r => r.RevisionDate,
            r => r.ExpirationDate,
        ]
    );

    /// <summary>
    /// Converts an IEnumerable of Receives to a DataTable
    /// </summary>
    /// <remarks>Contains a hardcoded list of properties and must be updated with model</remarks>
    /// <param name="receives">List of receives</param>
    /// <returns>A data table matching the schema of dbo.Receive containing one row mapped from the items in <see cref="Receive"/>s</returns>
    public static DataTable ToDataTable(this IEnumerable<Receive> receives)
    {
        return _receiveTableBuilder.Build(receives ?? []);
    }
}
