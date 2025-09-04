using System.Data;
using System.Data.Common;

namespace Bit.Core.Repositories;

public interface ISqlTransactionProvider
{
    Task<DbTransaction> GetTransactionAsync(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted);
}
