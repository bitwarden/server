﻿using Duende.IdentityServer.Models;

namespace Bit.Core.IdentityServer;

public static class ApiScopes
{
    public const string Api = "api";
    public const string ApiInstallation = "api.installation";
    public const string ApiLicensing = "api.licensing";
    public const string ApiOrganization = "api.organization";
    public const string ApiPush = "api.push";
    public const string ApiSecrets = "api.secrets";
    public const string Internal = "internal";
    public const string Send = "api.send";

    public static IEnumerable<ApiScope> GetApiScopes()
    {
        return new List<ApiScope>
        {
            new(Api, "API Access"),
            new(ApiPush, "API Push Access"),
            new(ApiLicensing, "API Licensing Access"),
            new(ApiOrganization, "API Organization Access"),
            new(ApiInstallation, "API Installation Access"),
            new(Internal, "Internal Access"),
            new(ApiSecrets, "Secrets Manager Access"),
            new(Send, "Send Access"),
        };
    }
}
