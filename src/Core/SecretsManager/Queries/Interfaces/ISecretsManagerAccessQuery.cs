namespace Bit.Core.SecretsManager.Queries.Interfaces;

public interface ISecretsManagerAccessQuery
{
    /// <summary>
    /// Determines whether the current caller may use Secrets Manager for the given organization.
    /// </summary>
    /// <remarks>
    /// The caller's token carries a Secrets Manager access claim that reflects the organization's state when the
    /// token was issued. This query also checks the organization's current state so access ends as soon as
    /// Secrets Manager is turned off or the organization is disabled, rather than when the token expires.
    /// </remarks>
    Task<bool> HasAccessAsync(Guid organizationId);
}
