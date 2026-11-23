using System.ComponentModel.DataAnnotations;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Repositories;
using Bit.Core.Repositories;
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
    IManglerService manglerService)
    : SsoConfigScene<OrganizationSsoScene.Request>(
        organizationRepository, ssoConfigRepository, policyRepository, manglerService)
{
    public class Request : SsoConfigScene<Request>.SsoConfigRequest
    {
        [Required]
        public required string Authority { get; set; }
        [Required]
        public required string ClientId { get; set; }
        [Required]
        public required string ClientSecret { get; set; }
        public string? MetadataAddress { get; set; }
        public OpenIdConnectRedirectBehavior RedirectBehavior { get; set; } = OpenIdConnectRedirectBehavior.RedirectGet;
        public bool GetClaimsFromUserInfoEndpoint { get; set; } = true;
    }

    protected override Task<SsoConfig> BuildSsoConfigAsync(Guid organizationId, Request request) =>
        Task.FromResult(SsoConfigSeeder.CreateOidc(
            organizationId,
            request.Authority,
            request.ClientId,
            request.ClientSecret,
            request.MetadataAddress,
            request.RedirectBehavior,
            request.GetClaimsFromUserInfoEndpoint,
            request.MemberDecryptionType));
}
