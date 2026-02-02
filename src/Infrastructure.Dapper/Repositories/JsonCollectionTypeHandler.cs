using System.Data;
using System.Text.Json;
using Dapper;

#nullable enable

namespace Bit.Infrastructure.Dapper.Repositories;

public class JsonCollectionTypeHandler : SqlMapper.TypeHandler<ICollection<string>?>
{
    public override void SetValue(IDbDataParameter parameter, ICollection<string>? value)
    {
        parameter.Value = value == null ? (object)DBNull.Value : JsonSerializer.Serialize(value);
    }

    public override ICollection<string>? Parse(object value)
    {
        if (value == null || value is DBNull)
        {
            return null;
        }

        var json = value.ToString();
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<List<string>>(json);
    }
}
