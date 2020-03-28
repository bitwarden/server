using System.Text.RegularExpressions;
using Dapper;

namespace Bit.Core.Repositories.PostgreSql
{
    public abstract class BasePostgreSqlRepository : BaseRepository
    {
        static BasePostgreSqlRepository()
        {
            // Support snake case property names
            DefaultTypeMap.MatchNamesWithUnderscores = true;
        }

        public BasePostgreSqlRepository(string connectionString, string readOnlyConnectionString)
            : base(connectionString, readOnlyConnectionString)
        { }

        protected static string SnakeCase(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return input;
            }
            var startUnderscores = Regex.Match(input, @"^_+");
            return startUnderscores + Regex.Replace(input, @"([a-z0-9])([A-Z])", "$1_$2").ToLowerInvariant();
        }

        protected static DynamicParameters ToParam<T>(T obj)
        {
            var dp = new DynamicParameters();
            var properties = typeof(T).GetProperties();
            foreach (var property in properties)
            {
                dp.Add($"_{SnakeCase(property.Name)}", property.GetValue(obj));
            }
            return dp;
        }
    }
}
