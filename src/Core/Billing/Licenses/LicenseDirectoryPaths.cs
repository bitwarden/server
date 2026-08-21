namespace Bit.Core.Billing.Licenses;

/// <summary>
/// Builds the on-disk paths for self-hosted license files. Kept in one place so writers, readers, and
/// cleanup share the exact same convention.
/// </summary>
public static class LicenseDirectoryPaths
{
    public static string UserLicenseDirectory(string licenseDirectory) => $"{licenseDirectory}/user";

    public static string UserLicensePath(string licenseDirectory, Guid userId) =>
        $"{UserLicenseDirectory(licenseDirectory)}/{userId}.json";
}
