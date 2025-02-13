﻿namespace Bit.Core.Identity;

public static class Claims
{
    // User
    public const string SecurityStamp = "sstamp";
    public const string Premium = "premium";
    public const string Device = "device";
    public const string DeviceType = "devicetype";

    public const string OrganizationOwner = "orgowner";
    public const string OrganizationAdmin = "orgadmin";
    public const string OrganizationUser = "orguser";
    public const string OrganizationCustom = "orgcustom";
    public const string ProviderAdmin = "providerprovideradmin";
    public const string ProviderServiceUser = "providerserviceuser";

    public const string SecretsManagerAccess = "accesssecretsmanager";

    // Service Account
    public const string Organization = "organization";

    // General
    public const string Type = "type";
}
