using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Bit.Core.Models.Data;
using Bit.Core.Models.Table;
using Dapper;

namespace Bit.Core.Utilities
{
    public static class TvpHelpers
    {
        internal static readonly Dictionary<Type, ITvpConverterFactory> _tvpConverterFactories =
            new Dictionary<Type, ITvpConverterFactory>
        {
            { typeof(OrganizationUser), BuildTvpConverterFactory<OrganizationUser>() }
        };

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

        public static DataTable ToTVP<T>(string tableTypeName, IEnumerable<T> values)
        {
            var tvpConverter = BuildTvpConverter<T>();
            tvpConverter.Table.SetTypeName(tableTypeName);
            tvpConverter.AddRows(values);
            return tvpConverter.Table;
        }

        private static TvpConverter<T> BuildTvpConverter<T>() =>
            ((TvpConverterFactory<T>)_tvpConverterFactories[typeof(T)]).MakeConverter();

        internal static TvpConverterFactory<T> BuildTvpConverterFactory<T>()
        {
            var table = new DataTable();

            var setters = new List<Action<DataRow, T>>();
            var ordinalGetters = new List<(int columnOrdinal, Delegate getter)>();
            foreach (var propertyInfo in typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(pi => Attribute.IsDefined(pi, typeof(DbOrderAttribute)))
                .OrderBy(pi => pi.GetCustomAttribute<DbOrderAttribute>().ParameterOrder))
            {
                var columnType = TypeToTableType(propertyInfo.PropertyType);
                var column = new DataColumn(propertyInfo.Name, columnType);

                if (columnType == typeof(DateTime))
                {
                    column.DateTimeMode = DataSetDateTime.Utc;
                }

                table.Columns.Add(column);

                // Create a compiled expression to get the parameter value
                // Used in creating table rows
                var arg = Expression.Parameter(typeof(T), "data");
                var exprGetValue = Expression.Property(arg, propertyInfo.Name);
                var exprConvert = Expression.Convert(exprGetValue, typeof(object));
                var compiledGetter = Expression.Lambda<Func<T, object>>(exprConvert, arg).Compile();
                setters.Add((row, data) => SetNullableField(row, column.Ordinal, compiledGetter(data)));
            }

            Action<T, DataTable> rowSetter = (T data, DataTable table) =>
            {
                var row = table.NewRow();
                foreach (var setter in setters)
                {
                    setter(row, data);
                }
                table.Rows.Add(row);
            };

            return new TvpConverterFactory<T>(table, rowSetter);
        }

        internal static void SetNullableField(this DataRow row, int columnOrdinal, object value)
        {
            if (value == null)
            {
                row[columnOrdinal] = DBNull.Value;
            }
            else
            {
                row[columnOrdinal] = value;
            }
        }

        private static Type TypeToTableType(Type type)
        {
            var underlyingType = Nullable.GetUnderlyingType(type);
            if (underlyingType != null)
            {
                return underlyingType;
            }
            if (type.IsEnum)
            {
                return typeof(byte);
            }
            return type;
        }
    }

    internal interface ITvpConverterFactory { }

    internal class TvpConverterFactory<T> : ITvpConverterFactory
    {
        private readonly Action<T, DataTable> _rowSetter;
        private readonly DataTable _tablePattern;

        public TvpConverterFactory(DataTable table, Action<T, DataTable> rowSetter)
        {
            _tablePattern = table;
            _rowSetter = rowSetter;
        }

        public TvpConverter<T> MakeConverter()
        {
            return new TvpConverter<T>(_tablePattern.Clone(), _rowSetter);
        }
    }

    internal class TvpConverter<T>
    {
        private readonly Action<T, DataTable> _rowSetter;
        public DataTable Table { get; }

        public TvpConverter(DataTable table, Action<T, DataTable> rowSetter)
        {
            Table = table;
            _rowSetter = rowSetter;
        }

        public void AddRow(T model) => AddRows(new[] { model });
        public void AddRows(IEnumerable<T> models)
        {
            foreach (var model in models)
            {
                _rowSetter(model, Table);
            }
        }
    }
}
