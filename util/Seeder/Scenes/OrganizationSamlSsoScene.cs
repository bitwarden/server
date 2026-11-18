using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Repositories;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Bit.Seeder.Data.Static;
using Bit.Seeder.Extensions;
using Bit.Seeder.Factories;
using Bit.Seeder.Services;
using Bit.Seeder.Steps;

namespace Bit.Seeder.Scenes;

/// <summary>
/// Makes an existing organization SAML 2.0 SSO-enabled against the local dev IdP. Writes the same organization
/// and policy state as <see cref="OrganizationSsoScene"/>, with a SAML config instead of an OpenID Connect one.
/// </summary>
public class OrganizationSamlSsoScene(
    IOrganizationRepository organizationRepository,
    ISsoConfigRepository ssoConfigRepository,
    IPolicyRepository policyRepository,
    IManglerService manglerService) : IScene<OrganizationSamlSsoScene.Request, OrganizationSsoSceneResult>
{
    private static readonly PolicyType[] _trustedDeviceEncryptionPolicyTypes =
        [PolicyType.SingleOrg, PolicyType.ResetPassword, PolicyType.RequireSso];

    // CreateSsoConfigStep keeps its cert fetch private. Resolving it once here reuses that code without editing the step.
    // A rename fails at type initialization, so every scene test reports it.
    private static readonly Func<string, Task<string>> _fetchIdpSigningCertificate = ResolveFetchIdpSigningCertificate();

    public class Request
    {
        [Required]
        public required Guid OrganizationId { get; set; }
        [Required]
        public required string Identifier { get; set; }
        public MemberDecryptionType MemberDecryptionType { get; set; } = MemberDecryptionType.MasterPassword;
        // Master-password orgs only; TDE always writes its policies.
        public bool RequireSsoLoginPolicy { get; set; }
        // Overrides the IdP signing cert; when omitted it is read from the live IdP metadata.
        public string? IdpX509PublicCert { get; set; }
    }

    public async Task<SceneResult<OrganizationSsoSceneResult>> SeedAsync(Request request)
    {
        var organization = await organizationRepository.GetOrganizationForSsoSeedOrThrowAsync(
            ssoConfigRepository, request.OrganizationId);

        // Built before any write so an unreachable IdP leaves the organization untouched.
        var ssoConfig = await BuildSamlConfigAsync(organization.Id, request);

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

    private static async Task<SsoConfig> BuildSamlConfigAsync(Guid organizationId, Request request)
    {
        var cert = request.IdpX509PublicCert;
        var signingCertificate = string.IsNullOrWhiteSpace(cert)
            ? await _fetchIdpSigningCertificate(LocalSamlIdp.EntityId)
            : cert;

        return SsoConfigSeeder.Create(
            organizationId,
            LocalSamlIdp.EntityId,
            LocalSamlIdp.SingleSignOnServiceUrl,
            signingCertificate,
            request.MemberDecryptionType);
    }

    private static Func<string, Task<string>> ResolveFetchIdpSigningCertificate()
    {
        var method = typeof(CreateSsoConfigStep).GetMethod(
            "FetchIdpSigningCertificateAsync", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException(
                "CreateSsoConfigStep.FetchIdpSigningCertificateAsync not found; update OrganizationSamlSsoScene.");

        return (Func<string, Task<string>>)Delegate.CreateDelegate(typeof(Func<string, Task<string>>), method);
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
