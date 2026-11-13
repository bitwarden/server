namespace Bit.Test.Common.Helpers;

public static class TestContainerImages
{
    // Renovate updates the tag and the digest of this value (see .github/renovate.json5).
    // Keep the value on one line, in the form "image:tag@sha256:digest".
    public const string Azurite =
        "mcr.microsoft.com/azure-storage/azurite:3.37.0@sha256:830430c1da1a2d537e08f3e6764dd1f5ae00cf0346bcaf625b968ec3f0971fd5";
}
