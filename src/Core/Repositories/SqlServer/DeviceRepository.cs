using System;
using System.Linq;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Bit.Core.Domains;
using System.Data;
using Dapper;

namespace Bit.Core.Repositories.SqlServer
{
    public class DeviceRepository : Repository<Device, Guid>, IDeviceRepository
    {
        public DeviceRepository(GlobalSettings globalSettings)
            : this(globalSettings.SqlServer.ConnectionString)
        { }

        public DeviceRepository(string connectionString)
            : base(connectionString)
        { }

        public async Task<Device> GetByIdAsync(Guid id, Guid userId)
        {
            var device = await GetByIdAsync(id);
            if(device == null || device.UserId != userId)
            {
                return null;
            }

            return device;
        }

        public async Task<ICollection<Device>> GetManyByUserIdAsync(Guid userId)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.QueryAsync<Device>(
                    $"[{Schema}].[{Table}_ReadByUserId]",
                    new { UserId = userId },
                    commandType: CommandType.StoredProcedure);

                return results.ToList();
            }
        }
    }
}
