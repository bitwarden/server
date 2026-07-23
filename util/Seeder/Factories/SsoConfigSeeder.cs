using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Data;

namespace Bit.Seeder.Factories;

internal static class SsoConfigSeeder
{
    internal static SsoConfig CreateSaml2(
        Guid organizationId,
        string idpEntityId,
        string idpSingleSignOnServiceUrl,
        string idpX509PublicCert,
        MemberDecryptionType memberDecryptionType = MemberDecryptionType.MasterPassword)
    {
        var data = new SsoConfigurationData
        {
            ConfigType = SsoType.Saml2,
            MemberDecryptionType = memberDecryptionType,
            IdpEntityId = idpEntityId,
            IdpSingleSignOnServiceUrl = idpSingleSignOnServiceUrl,
            IdpX509PublicCert = idpX509PublicCert,
            // Saml2BindingType has no 0 member, so the CLR default is invalid — set it explicitly.
            IdpBindingType = Saml2BindingType.HttpRedirect,
            // Per-org SP entity id (…/saml2/{orgId}), matching the Admin Console default and the
            // dev/.env IDP_SP_ENTITY_ID wiring. Without it the SP entity id is the base …/saml2 and
            // the IdP rejects the AuthnRequest ("metadata not found").
            SpUniqueEntityId = true,
        };

        var ssoConfig = new SsoConfig
        {
            OrganizationId = organizationId,
            Enabled = true,
        };
        ssoConfig.SetData(data);

        return ssoConfig;
    }
}
