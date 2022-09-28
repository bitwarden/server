using System.Data;
using Bit.Core.Entities;
using Bit.Core.Models.Data;
using Dapper;

namespace Bit.Infrastructure.Dapper;

public static class DapperHelpers
{
    public static DataTable ToGuidIdArrayTVP(this IEnumerable<Guid> ids)
    {
        return ids.ToArrayTVP("GuidId");
    }

    public static DataTable ToArrayTVP<T>(this IEnumerable<T> values, string columnName)
    {
        var table = new DataTable();
        table.SetTypeName($"[dbo].[{columnName}Array]");
        table.Columns.Add(columnName, typeof(T));

        if (values != null)
        {
            foreach (var value in values)
            {
                table.Rows.Add(value);
            }
        }

        return table;
    }

    public static DataTable ToArrayTVP(this IEnumerable<SelectionReadOnly> values)
    {
        var table = new DataTable();
        table.SetTypeName("[dbo].[SelectionReadOnlyArray]");

        var idColumn = new DataColumn("Id", typeof(Guid));
        table.Columns.Add(idColumn);
        var readOnlyColumn = new DataColumn("ReadOnly", typeof(bool));
        table.Columns.Add(readOnlyColumn);
        var hidePasswordsColumn = new DataColumn("HidePasswords", typeof(bool));
        table.Columns.Add(hidePasswordsColumn);

        if (values != null)
        {
            foreach (var value in values)
            {
                var row = table.NewRow();
                row[idColumn] = value.Id;
                row[readOnlyColumn] = value.ReadOnly;
                row[hidePasswordsColumn] = value.HidePasswords;
                table.Rows.Add(row);
            }
        }

        return table;
    }

    public static DataTable ToTvp(this IEnumerable<OrganizationUser> orgUsers)
    {
        var table = new DataTable();
        table.SetTypeName("[dbo].[OrganizationUserType]");

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
        };

        return orgUsers.BuildTable(table, columnData);
    }

    public static DataTable ToTvp(this IEnumerable<OrganizationSponsorship> organizationSponsorships)
    {
        var table = new DataTable();
        table.SetTypeName("[dbo].[OrganizationSponsorshipType]");

        var columnData = new List<(string name, Type type, Func<OrganizationSponsorship, object> getter)>
        {
            (nameof(OrganizationSponsorship.Id), typeof(Guid), ou => ou.Id),
            (nameof(OrganizationSponsorship.SponsoringOrganizationId), typeof(Guid), ou => ou.SponsoringOrganizationId),
            (nameof(OrganizationSponsorship.SponsoringOrganizationUserId), typeof(Guid), ou => ou.SponsoringOrganizationUserId),
            (nameof(OrganizationSponsorship.SponsoredOrganizationId), typeof(Guid), ou => ou.SponsoredOrganizationId),
            (nameof(OrganizationSponsorship.FriendlyName), typeof(string), ou => ou.FriendlyName),
            (nameof(OrganizationSponsorship.OfferedToEmail), typeof(string), ou => ou.OfferedToEmail),
            (nameof(OrganizationSponsorship.PlanSponsorshipType), typeof(byte), ou => ou.PlanSponsorshipType),
            (nameof(OrganizationSponsorship.LastSyncDate), typeof(DateTime), ou => ou.LastSyncDate),
            (nameof(OrganizationSponsorship.ValidUntil), typeof(DateTime), ou => ou.ValidUntil),
            (nameof(OrganizationSponsorship.ToDelete), typeof(bool), ou => ou.ToDelete),
        };

        return organizationSponsorships.BuildTable(table, columnData);
    }

    private static DataTable BuildTable<T>(this IEnumerable<T> entities, DataTable table, List<(string name, Type type, Func<T, object> getter)> columnData)
    {
        foreach (var (name, type, getter) in columnData)
        {
            var column = new DataColumn(name, type);
            table.Columns.Add(column);
        }

        foreach (var entity in entities ?? new T[] { })
        {
            var row = table.NewRow();
            foreach (var (name, type, getter) in columnData)
            {
                var val = getter(entity);
                if (val == null)
                {
                    row[name] = DBNull.Value;
                }
                else
                {
                    row[name] = val;
                }
            }
            table.Rows.Add(row);
        }

        return table;
    }
}
