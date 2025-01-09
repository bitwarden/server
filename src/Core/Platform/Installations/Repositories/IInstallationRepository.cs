using Bit.Core.Repositories;

#nullable enable

namespace Bit.Core.Platform.Installations;

/// <summary>
/// The CRUD repository interface for communicating with `dbo.Installation`,
/// which is used to store information pertinent to self-hosted
/// installations.
/// </summary>
/// <remarks>
/// This interface is implemented by `InstallationRepository` in the Dapper
/// and Entity Framework projects.
/// </remarks>
/// <seealso cref="T:Bit.Infrastructure.Dapper.Platform.Installations.Repositories.InstallationRepository"/>
public interface IInstallationRepository : IRepository<Installation, Guid>
{
}
