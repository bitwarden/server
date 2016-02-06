using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Bit.Core.Repositories.SqlServer
{
    public class CipherRepository : ICipherRepository
    {
        public CipherRepository(string connectionString)
        { }

        public Task DirtyCiphersAsync(string userId)
        {
            throw new NotImplementedException();
        }

        public Task UpdateDirtyCiphersAsync(IEnumerable<dynamic> ciphers)
        {
            throw new NotImplementedException();
        }

        public Task CreateAsync(IEnumerable<dynamic> ciphers)
        {
            throw new NotImplementedException();
        }
    }
}
