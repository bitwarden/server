using System;
using System.Data;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;

namespace Bit.Infrastructure.Dapper.Repositories
{
    public class JsonElementHandler : SqlMapper.TypeHandler<JsonElement>
    {
        public override JsonElement Parse(object value)
        {
            Debug.Assert(value.GetType() == typeof(string));
            var doc = JsonDocument.Parse((string)value);
            return doc.RootElement;
        }

        public override void SetValue(IDbDataParameter parameter, JsonElement value)
        {
            parameter.DbType = DbType.String;
            parameter.Value = JsonSerializer.Serialize(value, new JsonSerializerOptions
            {
                WriteIndented = false,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            });
        }
    }
}
