using System.Text.Json;
using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.Exceptions;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Models.Data;
using Bit.Core.Vault.Repositories;
using Bitwarden.Server.Sdk.Features;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Pam.Services;

public class PartialCipherWriteGuard : IPartialCipherWriteGuard
{
    private readonly IFeatureService _featureService;
    private readonly IOrganizationAbilityCacheService _organizationAbilityCacheService;
    private readonly ICipherRepository _cipherRepository;
    private readonly ILogger<PartialCipherWriteGuard> _logger;

    public PartialCipherWriteGuard(
        IFeatureService featureService,
        IOrganizationAbilityCacheService organizationAbilityCacheService,
        ICipherRepository cipherRepository,
        ILogger<PartialCipherWriteGuard> logger)
    {
        _featureService = featureService;
        _organizationAbilityCacheService = organizationAbilityCacheService;
        _cipherRepository = cipherRepository;
        _logger = logger;
    }

    private bool Enabled =>
        _featureService.IsEnabled(FeatureFlagKeys.Pam) &&
        !_featureService.IsEnabled(FeatureFlagKeys.PamDisablePartialCipherWriteGuard);

    public async Task EnsureNotPartialShapedAsync(Cipher cipher)
    {
        if (cipher.Id == default || cipher.OrganizationId is not { } organizationId || !Enabled ||
            cipher.IsDataBlobEncrypted())
        {
            return;
        }

        var ability = await _organizationAbilityCacheService.GetOrganizationAbilityAsync(organizationId);
        if (ability?.UsePam != true || HasSecretContent(cipher.Data))
        {
            return;
        }

        var stored = await _cipherRepository.GetByIdAsync(cipher.Id);
        if (stored == null || stored.IsDataBlobEncrypted() || !HasSecretContent(stored.Data))
        {
            return;
        }

        _logger.LogInformation("Refused a partial-shaped write to cipher {CipherId} in organization {OrganizationId}",
            cipher.Id, organizationId);
        throw new BadRequestException(
            "This item could not be saved because this client does not hold its full content. " +
            "Sync your vault and try again. If the item is privileged, open it with an active access lease.");
    }

    /// <summary>
    /// Whether the JSON <c>Data</c> blob holds any non-empty encrypted value beyond what a partial keeps.
    /// </summary>
    internal static bool HasSecretContent(string? data) =>
        TopLevelProperties(data).Any(p => !PartialCipherData.KeptKeys.Contains(p.Name) && HasNonEmptyValue(p.Value));

    private static IEnumerable<JsonProperty> TopLevelProperties(string? data) =>
        !string.IsNullOrWhiteSpace(data) &&
        JsonSerializer.Deserialize<JsonElement>(data) is { ValueKind: JsonValueKind.Object } root
            ? root.EnumerateObject()
            : [];

    // Dates (PasswordRevisionDate, LastUsedDate, CreationDate) are plaintext metadata, not secrets.
    private static bool HasNonEmptyValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject().Any(p => HasNonEmptyValue(p.Value)),
        JsonValueKind.Array => element.EnumerateArray().Any(HasNonEmptyValue),
        JsonValueKind.String => element.GetString() is { Length: > 0 } && !element.TryGetDateTime(out _),
        _ => false,
    };
}
