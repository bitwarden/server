using System.ComponentModel.DataAnnotations;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Repositories;
using Bit.Seeder.Extensions;
using Bit.Seeder.Factories;
using Bit.Seeder.Services;

namespace Bit.Seeder.Scenes;

/// <summary>
/// Mints a Secrets Manager access token (an <see cref="Bit.Core.SecretsManager.Entities.ApiKey"/>) for an
/// existing service account, returning the assembled <c>0.{id}.{secret}:{key}</c> token string a bws client
/// can decode. Compose after <see cref="OrganizationServiceAccountScene"/>; read/write access is granted
/// separately via <see cref="OrganizationAccessPolicyScene"/>.
/// </summary>
public class OrganizationAccessTokenScene(
    IOrganizationRepository organizationRepository,
    IServiceAccountRepository serviceAccountRepository,
    IApiKeyRepository apiKeyRepository,
    IManglerService manglerService) : IScene<OrganizationAccessTokenScene.Request, OrganizationAccessTokenScene.Result>
{
    public class Request
    {
        [Required]
        public required Guid OrganizationId { get; set; }
        [Required]
        public required string OrganizationKeyB64 { get; set; }
        [Required]
        public required Guid ServiceAccountId { get; set; }
        [Required]
        public required string Name { get; set; }
    }

    public class Result
    {
        public required string AccessToken { get; init; }
        public required Guid ApiKeyId { get; init; }
    }

    public async Task<SceneResult<Result>> SeedAsync(Request request)
    {
        await organizationRepository.GetSecretsManagerOrganizationOrThrowAsync(request.OrganizationId);
        await serviceAccountRepository.ThrowIfServiceAccountsNotInOrganizationAsync(
            [request.ServiceAccountId], request.OrganizationId);

        var (apiKey, clientSecret, encryptionKeyB64) = AccessTokenSeeder.Create(
            request.OrganizationKeyB64, request.ServiceAccountId, request.Name);

        var created = await apiKeyRepository.CreateAsync(apiKey);

        var token = $"0.{created.Id}.{clientSecret}:{encryptionKeyB64}";

        return new SceneResult<Result>(
            result: new Result
            {
                AccessToken = token,
                ApiKeyId = created.Id
            },
            mangleMap: manglerService.GetMangleMap());
    }
}
