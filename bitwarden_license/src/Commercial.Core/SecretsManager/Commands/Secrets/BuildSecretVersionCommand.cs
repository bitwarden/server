using Bit.Core.Auth.Identity;
using Bit.Core.Context;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Commands.Secrets.Interfaces;
using Bit.Core.SecretsManager.Entities;

namespace Bit.Commercial.Core.SecretsManager.Commands.Secrets;

public class BuildSecretVersionCommand : IBuildSecretVersionCommand
{
    private readonly ICurrentContext _currentContext;
    private readonly IOrganizationUserRepository _organizationUserRepository;

    public BuildSecretVersionCommand(
        ICurrentContext currentContext,
        IOrganizationUserRepository organizationUserRepository)
    {
        _currentContext = currentContext;
        _organizationUserRepository = organizationUserRepository;
    }

    /// <summary>
    /// Records the secret's value as written, together with its revision date and the caller that
    /// wrote it, so each version reads as "this value, set at this time, by this editor".
    /// </summary>
    /// <remarks>
    /// This deliberately does not persist anything. Resolving the editor needs request context, so
    /// it cannot live in the repository, but the version must be written inside the same
    /// transaction as the secret — a separate write would leave the caller with an error for a
    /// secret that was already saved, prompting a retry that duplicates it.
    /// </remarks>
    public async Task<SecretVersion> BuildAsync(Secret secret, Guid accessClientId)
    {
        var (editorServiceAccountId, editorOrganizationUserId) =
            await ResolveEditorAsync(secret.OrganizationId, accessClientId);

        return new SecretVersion
        {
            SecretId = secret.Id,
            Value = secret.Value!,
            VersionDate = secret.RevisionDate,
            EditorServiceAccountId = editorServiceAccountId,
            EditorOrganizationUserId = editorOrganizationUserId
        };
    }

    /// <summary>
    /// Service accounts are recorded by their own id; members are recorded by their OrganizationUser id
    /// so attribution stays scoped to the organization.
    /// </summary>
    /// <remarks>
    /// Both ids stay null when the caller cannot be attributed to either — an organization API key
    /// authenticates as the organization itself, so it matches no OrganizationUser. That surfaces as an
    /// unknown editor rather than failing the write, which would otherwise block those clients entirely.
    /// </remarks>
    private async Task<(Guid? ServiceAccountId, Guid? OrganizationUserId)> ResolveEditorAsync(
        Guid organizationId, Guid accessClientId)
    {
        if (_currentContext.IdentityClientType == IdentityClientType.ServiceAccount)
        {
            return (accessClientId, null);
        }

        var organizationUser = await _organizationUserRepository.GetByOrganizationAsync(organizationId, accessClientId);
        return (null, organizationUser?.Id);
    }
}
