namespace Bit.Core.Platform.Installations;

/// <summary>
/// Query interface responsible for fetching an installation from
/// `InstallationRepository`.
/// </summary>
/// <remarks>
/// This interface is implemented by `GetInstallationQuery`
/// </remarks>
/// <seealso cref="GetInstallationQuery"/>
public interface IGetInstallationQuery
{
    /// <summary>
    /// Retrieves an installation from the `InstallationRepository` by its id.
    /// </summary>
    /// <param name="installationId">The GUID id of the installation.</param>
    /// <returns>A task containing an `Installation`.</returns>
    /// <seealso cref="T:Bit.Core.Platform.Installations.Repositories.IInstallationRepository"/>
    Task<Installation> GetByIdAsync(Guid installationId);
}
