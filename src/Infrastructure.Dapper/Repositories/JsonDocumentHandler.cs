using System;
using System.Data;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;

namespace Bit.Infrastructure.Dapper.Repositories
{
    public class JsonDocumentHandler : SqlMapper.TypeHandler<JsonDocument>
    {
        public override JsonDocument Parse(object value)
        {
            Debug.Assert(value.GetType() == typeof(string));
            return JsonDocument.Parse((string)value);
            
        }

        public override void SetValue(IDbDataParameter parameter, JsonDocument value)
        {
            parameter.DbType = DbType.String;
            parameter.Value = JsonSerializer.Serialize(value, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            });
        }
    }
}
