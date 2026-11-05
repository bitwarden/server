using System.ComponentModel.DataAnnotations;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Repositories;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
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
    }

    public async Task<SceneResult<OrganizationSsoSceneResult>> SeedAsync(Request request)
    {
        var organization = await organizationRepository.GetByIdAsync(request.OrganizationId)
            ?? throw new InvalidOperationException($"Organization {request.OrganizationId} not found.");

        if (await ssoConfigRepository.GetByOrganizationIdAsync(organization.Id) is not null)
        {
            throw new InvalidOperationException($"Organization {organization.Id} already has an SSO configuration.");
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

        if (request.MemberDecryptionType == MemberDecryptionType.TrustedDeviceEncryption)
        {
            await CreateTrustedDeviceEncryptionPoliciesAsync(organization.Id);
        }

        return new SceneResult<OrganizationSsoSceneResult>(
            result: new OrganizationSsoSceneResult
            {
                Identifier = identifier
            },
            mangleMap: manglerService.GetMangleMap());
    }

    private async Task CreateTrustedDeviceEncryptionPoliciesAsync(Guid organizationId)
    {
        var resetPassword = NewEnabledPolicy(organizationId, PolicyType.ResetPassword);
        resetPassword.SetDataModel(new ResetPasswordDataModel { AutoEnrollEnabled = true });

        await policyRepository.CreateAsync(NewEnabledPolicy(organizationId, PolicyType.SingleOrg));
        await policyRepository.CreateAsync(resetPassword);
        await policyRepository.CreateAsync(NewEnabledPolicy(organizationId, PolicyType.RequireSso));
    }

    private static Policy NewEnabledPolicy(Guid organizationId, PolicyType type) => new()
    {
        Id = CombGuid.Generate(),
        OrganizationId = organizationId,
        Type = type,
        Enabled = true,
    };
}
