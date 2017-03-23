using System;
using System.Threading.Tasks;
using Bit.Core.Models.Table;
using System.Collections.Generic;

namespace Bit.Core.Repositories
{
    public interface ISubvaultCipherRepository
    {
        Task<ICollection<SubvaultCipher>> GetManyByUserIdAsync(Guid userId);
    }
}
