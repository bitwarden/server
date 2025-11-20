using Bit.Core.Entities;

namespace Bit.Core.KeyManagement.Queries.Interfaces;

public interface IGetMinimumClientVersionForUserQuery
{
    Task<Version?> Run(User? user);
}
