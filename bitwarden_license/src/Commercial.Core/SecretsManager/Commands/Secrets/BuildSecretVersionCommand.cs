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
