namespace Bit.Core.Platform.Installations;

/// <summary>
/// Commands responsible for updating an installation from
/// `InstallationRepository`.
/// </summary>
/// <remarks>
/// If referencing: you probably want the interface
/// `IUpdateInstallationCommand` instead of directly calling this class.
/// </remarks>
/// <seealso cref="IUpdateInstallationCommand"/>
public class UpdateInstallationCommand : IUpdateInstallationCommand
{
    private readonly IGetInstallationQuery _getInstallationQuery;
    private readonly IInstallationRepository _installationRepository;
    private readonly TimeProvider _timeProvider;

    public UpdateInstallationCommand(
        IGetInstallationQuery getInstallationQuery,
        IInstallationRepository installationRepository,
        TimeProvider timeProvider
    )
    {
        _getInstallationQuery = getInstallationQuery;
        _installationRepository = installationRepository;
        _timeProvider = timeProvider;
    }

    public async Task UpdateLastActivityDateAsync(Guid installationId)
    {
        if (installationId == default)
        {
            throw new Exception
            (
              "Tried to update the last activity date for " +
              "an installation, but an invalid installation id was " +
              "provided."
            );
        }
        var installation = await _getInstallationQuery.GetByIdAsync(installationId);
        if (installation == null)
        {
            throw new Exception
            (
              "Tried to update the last activity date for " +
              $"installation {installationId.ToString()}, but no " +
              "installation was found for that id."
            );
        }
        installation.LastActivityDate = _timeProvider.GetUtcNow().UtcDateTime;
        await _installationRepository.UpsertAsync(installation);
    }
}
