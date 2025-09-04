﻿using Bit.Core.Auth.Identity;
using Bit.Core.Auth.IdentityServer;
using Duende.IdentityModel;
using Duende.IdentityServer.Models;

namespace Bit.Identity.IdentityServer;

public class ApiResources
{
    public static IEnumerable<ApiResource> GetApiResources()
    {
        return new List<ApiResource>
        {
            new("api", new[] {
                JwtClaimTypes.Name,
                JwtClaimTypes.Email,
                JwtClaimTypes.EmailVerified,
                Claims.SecurityStamp,
                Claims.Premium,
                Claims.Device,
                Claims.DeviceType,
                Claims.OrganizationOwner,
                Claims.OrganizationAdmin,
                Claims.OrganizationUser,
                Claims.OrganizationCustom,
                Claims.ProviderAdmin,
                Claims.ProviderServiceUser,
                Claims.SecretsManagerAccess
            }),
            new(ApiScopes.ApiSendAccess, [
                JwtClaimTypes.Subject,
                Claims.SendAccessClaims.SendId
            ]),
            new(ApiScopes.Internal, new[] { JwtClaimTypes.Subject }),
            new(ApiScopes.ApiPush, new[] { JwtClaimTypes.Subject }),
            new(ApiScopes.ApiLicensing, new[] { JwtClaimTypes.Subject }),
            new(ApiScopes.ApiOrganization, new[] { JwtClaimTypes.Subject }),
            new(ApiScopes.ApiInstallation, new[] { JwtClaimTypes.Subject }),
            new(ApiScopes.ApiSecrets, new[] { JwtClaimTypes.Subject, Claims.Organization }),
        };
    }
}
