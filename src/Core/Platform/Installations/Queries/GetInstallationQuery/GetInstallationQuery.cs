namespace Bit.Core.Platform.Installations;

/// <summary>
/// Queries responsible for fetching an installation from
/// `InstallationRepository`.
/// </summary>
/// <remarks>
/// If referencing: you probably want the interface `IGetInstallationQuery`
/// instead of directly calling this class.
/// </remarks>
/// <seealso cref="IGetInstallationQuery"/>
public class GetInstallationQuery : IGetInstallationQuery
{
    private readonly IInstallationRepository _installationRepository;

    public GetInstallationQuery(IInstallationRepository installationRepository)
    {
        _installationRepository = installationRepository;
    }

    /// <inheritdoc cref="IGetInstallationQuery.GetByIdAsync"/>
    public async Task<Installation> GetByIdAsync(Guid installationId)
    {
        if (installationId == default(Guid))
        {
            return null;
        }
        return await _installationRepository.GetByIdAsync(installationId);
    }
}
