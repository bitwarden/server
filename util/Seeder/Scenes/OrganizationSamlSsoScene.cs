using System.Reflection;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Repositories;
using Bit.Core.Repositories;
using Bit.Seeder.Data.Static;
using Bit.Seeder.Factories;
using Bit.Seeder.Services;
using Bit.Seeder.Steps;

namespace Bit.Seeder.Scenes;

public class OrganizationSamlSsoScene(
    IOrganizationRepository organizationRepository,
    ISsoConfigRepository ssoConfigRepository,
    IPolicyRepository policyRepository,
    IManglerService manglerService)
    : SsoConfigScene<OrganizationSamlSsoScene.Request>(
        organizationRepository, ssoConfigRepository, policyRepository, manglerService)
{
    private static readonly Func<string, Task<string>> _fetchIdpSigningCertificate = ResolveFetchIdpSigningCertificate();

    public class Request : SsoConfigScene<Request>.SsoConfigRequest
    {
        public string? IdpX509PublicCert { get; set; }
    }

    protected override async Task<SsoConfig> BuildSsoConfigAsync(Guid organizationId, Request request)
    {
        var cert = request.IdpX509PublicCert;
        var signingCertificate = string.IsNullOrWhiteSpace(cert)
            ? await _fetchIdpSigningCertificate(LocalSamlIdp.EntityId)
            : cert;

        return SsoConfigSeeder.CreateLocalSaml(organizationId, signingCertificate, request.MemberDecryptionType);
    }

    private static Func<string, Task<string>> ResolveFetchIdpSigningCertificate()
    {
        var method = typeof(CreateSsoConfigStep).GetMethod(
            "FetchIdpSigningCertificateAsync", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException(
                "CreateSsoConfigStep.FetchIdpSigningCertificateAsync not found; update OrganizationSamlSsoScene.");

        return (Func<string, Task<string>>)Delegate.CreateDelegate(typeof(Func<string, Task<string>>), method);
    }
}
