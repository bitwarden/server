using System.Collections.Frozen;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using Bit.Core.Entities;
using Bit.Core.Models.Data;
using Dapper;

#nullable enable

namespace Bit.Infrastructure.Dapper;

/// <summary>
/// Provides a way to build a <see cref="DataTable"/> based on the properties of <see cref="T"/>.
/// </summary>
/// <typeparam name="T"></typeparam>
public class DataTableBuilder<T>
{
    private readonly FrozenDictionary<string, (Type Type, Func<T, object?> Getter)> _columnBuilders;

    /// <summary>
    /// Creates a new instance of <see cref="DataTableBuilder{T}"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// new DataTableBuilder<MyObject>(
    ///     [
    ///         i => i.Id,
    ///         i => i.Name,
    ///     ]
    /// );
    /// </code>
    /// </example>
    /// <param name="columnExpressions"></param>
    /// <exception cref="ArgumentException"></exception>
    public DataTableBuilder(Expression<Func<T, object?>>[] columnExpressions)
    {
        ArgumentNullException.ThrowIfNull(columnExpressions);
        ArgumentOutOfRangeException.ThrowIfZero(columnExpressions.Length);

        var columnBuilders = new Dictionary<string, (Type Type, Func<T, object?>)>(columnExpressions.Length);

        for (var i = 0; i < columnExpressions.Length; i++)
        {
            var columnExpression = columnExpressions[i];

            if (!TryGetPropertyInfo(columnExpression, out var propertyInfo))
            {
                throw new ArgumentException($"Could not determine the property info from the given expression '{columnExpression}'.");
            }

            // Unwrap possible Nullable<T>
            var type = Nullable.GetUnderlyingType(propertyInfo.PropertyType) ?? propertyInfo.PropertyType;

            // This needs to be after unwrapping the `Nullable` since enums can be nullable
            if (type.IsEnum)
            {
                // Get the backing type of the enum
                type = Enum.GetUnderlyingType(type);
            }

            if (!columnBuilders.TryAdd(propertyInfo.Name, (type, columnExpression.Compile())))
            {
                throw new ArgumentException($"Property with name '{propertyInfo.Name}' was already added, properties can only be added once.");
            }
        }

        _columnBuilders = columnBuilders.ToFrozenDictionary();
    }

    private static bool TryGetPropertyInfo(Expression<Func<T, object?>> columnExpression, [MaybeNullWhen(false)] out PropertyInfo property)
    {
        property = null;

        // Reference type properties
        // i => i.Data
        if (columnExpression.Body is MemberExpression { Member: PropertyInfo referencePropertyInfo })
        {
            property = referencePropertyInfo;
            return true;
        }

        // Value type properties will implicitly box into the object so 
        // we need to look past the Convert expression
        // i => (System.Object?)i.Id
        if (
            columnExpression.Body is UnaryExpression
            {
                NodeType: ExpressionType.Convert,
                Operand: MemberExpression { Member: PropertyInfo valuePropertyInfo },
            }
        )
        {
            // This could be an implicit cast from the property into our return type object?
            property = valuePropertyInfo;
            return true;
        }

        // Other possible expression bodies here
        return false;
    }

    public DataTable Build(IEnumerable<T> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var table = new DataTable();

        foreach (var (name, (type, _)) in _columnBuilders)
        {
            table.Columns.Add(new DataColumn(name, type));
        }

        foreach (var entity in source)
        {
            var row = table.NewRow();

            foreach (var (name, (_, getter)) in _columnBuilders)
            {
                var value = getter(entity);
                if (value is null)
                {
                    row[name] = DBNull.Value;
                }
                else
                {
                    row[name] = value;
                }
            }

            table.Rows.Add(row);
        }

        return table;
    }
}

public static class DapperHelpers
{
    private static readonly DataTableBuilder<OrganizationSponsorship> _organizationSponsorshipTableBuilder = new(
        [
            os => os.Id,
            os => os.SponsoringOrganizationId,
            os => os.SponsoringOrganizationUserId,
            os => os.SponsoredOrganizationId,
            os => os.FriendlyName,
            os => os.OfferedToEmail,
            os => os.PlanSponsorshipType,
            os => os.LastSyncDate,
            os => os.ValidUntil,
            os => os.ToDelete,
        ]
    );

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

    public static DataTable ToArrayTVP(this IEnumerable<CollectionAccessSelection> values)
    {
        var table = new DataTable();
        table.SetTypeName("[dbo].[CollectionAccessSelectionType]");

        var idColumn = new DataColumn("Id", typeof(Guid));
        table.Columns.Add(idColumn);
        var readOnlyColumn = new DataColumn("ReadOnly", typeof(bool));
        table.Columns.Add(readOnlyColumn);
        var hidePasswordsColumn = new DataColumn("HidePasswords", typeof(bool));
        table.Columns.Add(hidePasswordsColumn);
        var manageColumn = new DataColumn("Manage", typeof(bool));
        table.Columns.Add(manageColumn);

        if (values != null)
        {
            foreach (var value in values)
            {
                var row = table.NewRow();
                row[idColumn] = value.Id;
                row[readOnlyColumn] = value.ReadOnly;
                row[hidePasswordsColumn] = value.HidePasswords;
                row[manageColumn] = value.Manage;
                table.Rows.Add(row);
            }
        }

        return table;
    }

    public static DataTable ToTvp(this IEnumerable<OrganizationSponsorship> organizationSponsorships)
    {
        var table = _organizationSponsorshipTableBuilder.Build(organizationSponsorships ?? []);
        table.SetTypeName("[dbo].[OrganizationSponsorshipType]");
        return table;
    }

    public static DataTable BuildTable<T>(this IEnumerable<T> entities, DataTable table,
        List<(string name, Type type, Func<T, object?> getter)> columnData)
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
