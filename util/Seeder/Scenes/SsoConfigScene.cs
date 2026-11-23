using System.ComponentModel.DataAnnotations;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Repositories;
using Bit.Core.Repositories;
using Bit.Seeder.Extensions;
using Bit.Seeder.Factories;
using Bit.Seeder.Services;

namespace Bit.Seeder.Scenes;

public abstract class SsoConfigScene<TRequest>(
    IOrganizationRepository organizationRepository,
    ISsoConfigRepository ssoConfigRepository,
    IPolicyRepository policyRepository,
    IManglerService manglerService) : IScene<TRequest, OrganizationSsoSceneResult>
    where TRequest : SsoConfigScene<TRequest>.SsoConfigRequest
{
    private static readonly PolicyType[] _trustedDeviceEncryptionPolicyTypes =
        [PolicyType.SingleOrg, PolicyType.ResetPassword, PolicyType.RequireSso];

    public abstract class SsoConfigRequest
    {
        [Required]
        public required Guid OrganizationId { get; set; }
        [Required]
        public required string Identifier { get; set; }
        public MemberDecryptionType MemberDecryptionType { get; set; } = MemberDecryptionType.MasterPassword;
        // Master-password orgs only; TDE always writes its policies.
        public bool RequireSsoLoginPolicy { get; set; }
    }

    public async Task<SceneResult<OrganizationSsoSceneResult>> SeedAsync(TRequest request)
    {
        var organization = await organizationRepository.GetOrganizationForSsoSeedOrThrowAsync(
            ssoConfigRepository, request.OrganizationId);

        var ssoConfig = await BuildSsoConfigAsync(organization.Id, request);

        var policyTypes = PolicyTypesFor(request);

        var existingPolicies = new List<(PolicyType Type, Policy? Existing)>();
        foreach (var type in policyTypes)
        {
            existingPolicies.Add((type, await policyRepository.GetByOrganizationIdTypeAsync(organization.Id, type)));
        }

        var identifier = manglerService.Mangle(request.Identifier);
        organization.Identifier = identifier;
        organization.UseSso = true;
        await organizationRepository.ReplaceAsync(organization);

        await ssoConfigRepository.CreateAsync(ssoConfig);

        foreach (var (type, existing) in existingPolicies)
        {
            await UpsertEnabledPolicyAsync(organization.Id, type, existing);
        }

        return new SceneResult<OrganizationSsoSceneResult>(
            result: new OrganizationSsoSceneResult
            {
                Identifier = identifier
            },
            mangleMap: manglerService.GetMangleMap());
    }

    protected abstract Task<SsoConfig> BuildSsoConfigAsync(Guid organizationId, TRequest request);

    private static PolicyType[] PolicyTypesFor(SsoConfigRequest request)
    {
        if (request.MemberDecryptionType == MemberDecryptionType.TrustedDeviceEncryption)
        {
            return _trustedDeviceEncryptionPolicyTypes;
        }

        // SingleOrg must be enabled before RequireSso
        return request.RequireSsoLoginPolicy ? [PolicyType.SingleOrg, PolicyType.RequireSso] : [];
    }

    private async Task UpsertEnabledPolicyAsync(Guid organizationId, PolicyType type, Policy? existing)
    {
        if (existing is null)
        {
            var policy = PolicySeeder.Create(organizationId, type, enabled: true);
            SetTrustedDeviceEncryptionData(policy);
            await policyRepository.CreateAsync(policy);
            return;
        }

        existing.Enabled = true;
        SetTrustedDeviceEncryptionData(existing);
        await policyRepository.ReplaceAsync(existing);
    }

    private static void SetTrustedDeviceEncryptionData(Policy policy)
    {
        if (policy.Type == PolicyType.ResetPassword)
        {
            policy.SetDataModel(new ResetPasswordDataModel { AutoEnrollEnabled = true });
        }
    }
}
