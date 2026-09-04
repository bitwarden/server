#nullable enable
using Bit.Core.Auth.Identity;
using Bit.Core.Context;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Commands.Secrets.Interfaces;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;

namespace Bit.Commercial.Core.SecretsManager.Commands.Secrets;

public class CreateSecretVersionCommand : ICreateSecretVersionCommand
{
    private readonly ICurrentContext _currentContext;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly ISecretVersionRepository _secretVersionRepository;

    public CreateSecretVersionCommand(
        ICurrentContext currentContext,
        IOrganizationUserRepository organizationUserRepository,
        ISecretVersionRepository secretVersionRepository)
    {
        _currentContext = currentContext;
        _organizationUserRepository = organizationUserRepository;
        _secretVersionRepository = secretVersionRepository;
    }

    public async Task<SecretVersion> CreateAsync(Secret secret, Guid accessClientId)
    {
        var (editorServiceAccountId, editorOrganizationUserId) =
            await ResolveEditorAsync(secret.OrganizationId, accessClientId);

        return await _secretVersionRepository.CreateAsync(new SecretVersion
        {
            SecretId = secret.Id,
            Value = secret.Value!,
            VersionDate = secret.RevisionDate,
            EditorServiceAccountId = editorServiceAccountId,
            EditorOrganizationUserId = editorOrganizationUserId
        });
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
