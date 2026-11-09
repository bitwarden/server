using System.ComponentModel.DataAnnotations;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Repositories;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Bit.Seeder.Extensions;
using Bit.Seeder.Factories;
using Bit.Seeder.Services;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace Bit.Seeder.Scenes;

public struct OrganizationSsoSceneResult
{
    public string Identifier { get; init; }
}

public class OrganizationSsoScene(
    IOrganizationRepository organizationRepository,
    ISsoConfigRepository ssoConfigRepository,
    IPolicyRepository policyRepository,
    IManglerService manglerService) : IScene<OrganizationSsoScene.Request, OrganizationSsoSceneResult>
{
    private static readonly PolicyType[] _trustedDeviceEncryptionPolicyTypes =
        [PolicyType.SingleOrg, PolicyType.ResetPassword, PolicyType.RequireSso];

    public class Request
    {
        [Required]
        public required Guid OrganizationId { get; set; }
        [Required]
        public required string Identifier { get; set; }
        [Required]
        public required string Authority { get; set; }
        [Required]
        public required string ClientId { get; set; }
        [Required]
        public required string ClientSecret { get; set; }
        public string? MetadataAddress { get; set; }
        public OpenIdConnectRedirectBehavior RedirectBehavior { get; set; } = OpenIdConnectRedirectBehavior.RedirectGet;
        public bool GetClaimsFromUserInfoEndpoint { get; set; } = true;
        public MemberDecryptionType MemberDecryptionType { get; set; } = MemberDecryptionType.MasterPassword;
        public bool RequireSsoLoginPolicy { get; set; }
    }

    public async Task<SceneResult<OrganizationSsoSceneResult>> SeedAsync(Request request)
    {
        var organization = await organizationRepository.GetOrganizationForSsoSeedOrThrowAsync(
            ssoConfigRepository, request.OrganizationId);

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

        var ssoConfig = SsoConfigSeeder.CreateOidc(
            organization.Id,
            request.Authority,
            request.ClientId,
            request.ClientSecret,
            request.MetadataAddress,
            request.RedirectBehavior,
            request.GetClaimsFromUserInfoEndpoint,
            request.MemberDecryptionType);
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

    private static PolicyType[] PolicyTypesFor(Request request)
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
            var policy = NewEnabledPolicy(organizationId, type);
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

    private static Policy NewEnabledPolicy(Guid organizationId, PolicyType type) => new()
    {
        Id = CombGuid.Generate(),
        OrganizationId = organizationId,
        Type = type,
        Enabled = true,
    };
}
