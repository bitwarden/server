using Bit.Core.Platform.Installations;
using Bit.Core.Settings;
using Bit.Infrastructure.Dapper.Repositories;

#nullable enable

namespace Bit.Infrastructure.Dapper.Platform;

/// <summary>
/// The CRUD repository for communicating with `dbo.Installation`.
/// </summary>
/// <remarks>
/// If referencing: you probably want the interface `IInstallationRepository`
/// instead of directly calling this class.
/// </remarks>
/// <seealso cref="IInstallationRepository"/>
public class InstallationRepository : Repository<Installation, Guid>, IInstallationRepository
{
    public InstallationRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    { }

    public InstallationRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    { }
}
