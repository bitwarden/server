using Bit.Core;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Commands.Secrets.Interfaces;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Models.Data.AccessPolicyUpdates;
using Bit.Core.SecretsManager.Repositories;
using Bitwarden.Server.Sdk.Features;

namespace Bit.Commercial.Core.SecretsManager.Commands.Secrets;

public class CreateSecretCommand : ICreateSecretCommand
{
    private readonly ISecretRepository _secretRepository;
    private readonly IBuildSecretVersionCommand _buildSecretVersionCommand;
    private readonly ICurrentContext _currentContext;
    private readonly IFeatureService _featureService;

    public CreateSecretCommand(
        ISecretRepository secretRepository,
        IBuildSecretVersionCommand buildSecretVersionCommand,
        ICurrentContext currentContext,
        IFeatureService featureService)
    {
        _secretRepository = secretRepository;
        _buildSecretVersionCommand = buildSecretVersionCommand;
        _currentContext = currentContext;
        _featureService = featureService;
    }

    public async Task<Secret> CreateAsync(Secret secret, SecretAccessPoliciesUpdates? accessPoliciesUpdates)
    {
        var initialVersion = _featureService.IsEnabled(FeatureFlagKeys.SecretsVersioning)
            ? await _buildSecretVersionCommand.BuildAsync(secret, GetAccessClientId())
            : null;

        return await _secretRepository.CreateAsync(secret, accessPoliciesUpdates, initialVersion);
    }

    private Guid GetAccessClientId() =>
        _currentContext.UserId ??
        throw new BadRequestException("Cannot attribute a secret version to an unidentified client.");
}
