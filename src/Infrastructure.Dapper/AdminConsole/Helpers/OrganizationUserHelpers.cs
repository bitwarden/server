using System.Data;
using Bit.Core.Entities;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.Dapper.AdminConsole.Helpers;

public static class OrganizationUserHelpers
{
    public static DataTable ToTvp(this IEnumerable<OrganizationUser> orgUsers)
    {
        var table = new DataTable();
        table.SetTypeName("[dbo].[OrganizationUserType2]");

        var columnData = new List<(string name, Type type, Func<OrganizationUser, object> getter)>
        {
            (nameof(OrganizationUser.Id), typeof(Guid), ou => ou.Id),
            (nameof(OrganizationUser.OrganizationId), typeof(Guid), ou => ou.OrganizationId),
            (nameof(OrganizationUser.UserId), typeof(Guid), ou => ou.UserId),
            (nameof(OrganizationUser.Email), typeof(string), ou => ou.Email),
            (nameof(OrganizationUser.Key), typeof(string), ou => ou.Key),
            (nameof(OrganizationUser.Status), typeof(byte), ou => ou.Status),
            (nameof(OrganizationUser.Type), typeof(byte), ou => ou.Type),
            (nameof(OrganizationUser.AccessAll), typeof(bool), ou => ou.AccessAll),
            (nameof(OrganizationUser.ExternalId), typeof(string), ou => ou.ExternalId),
            (nameof(OrganizationUser.CreationDate), typeof(DateTime), ou => ou.CreationDate),
            (nameof(OrganizationUser.RevisionDate), typeof(DateTime), ou => ou.RevisionDate),
            (nameof(OrganizationUser.Permissions), typeof(string), ou => ou.Permissions),
            (nameof(OrganizationUser.ResetPasswordKey), typeof(string), ou => ou.ResetPasswordKey),
            (nameof(OrganizationUser.AccessSecretsManager), typeof(bool), ou => ou.AccessSecretsManager),
        };

        return orgUsers.BuildTable(table, columnData);
    }

    public static void UpdateEncryptedData(this IEnumerable<OrganizationUser> accountRecoveryKeys, Guid userId, SqlConnection connection, SqlTransaction transaction)
    {

        var sql = @"
                UPDATE
                    [dbo].[OrganizationUser]
                SET
                    [ResetPasswordKey] = AR.[ResetPasswordKey]
                FROM
                    [dbo].[OrganizationUser] OU
                INNER JOIN
                    @AccountRecoveryKeys AR ON OU.Id = AR.Id
                WHERE
                    OU.[UserId] = @UserId";

        var accountRecoveryTVP = accountRecoveryKeys.ToTvp();

        connection.Execute(
            sql,
            new { UserId = userId, AccountRecoveryKeys = accountRecoveryTVP },
            transaction: transaction,
            commandType: CommandType.Text);
    }

}
