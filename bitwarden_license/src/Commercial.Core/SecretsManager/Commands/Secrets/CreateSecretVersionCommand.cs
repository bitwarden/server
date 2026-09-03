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

    // Members are recorded by OrganizationUser id rather than User id, so attribution stays scoped to
    // this organization. Null is tolerated rather than fatal — the value change already happened, and
    // enforcing membership is the authorization layer's job, not this command's.
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
