namespace Bit.Test.Common.Helpers;

public static class TestContainerImages
{
    /// <summary>
    /// The Azure Storage SDK sets the storage API version in each request.
    /// Azurite rejects a request with an API version that is newer than the image knows.
    /// A floating tag such as <c>latest</c> can change which requests Azurite accepts, so results can change.
    /// The data protection, Identity certificate, and SSO certificate startup tests start this image.
    /// The tag and the digest pin one exact image.
    /// Renovate changes the pin in a pull request, and that pull request runs all three suites on the new image.
    /// </summary>
    // Renovate updates the tag and the digest of this value (see .github/renovate.json5).
    // Keep the value on one line, in the form "image:tag@sha256:digest".
    public const string Azurite =
        "mcr.microsoft.com/azure-storage/azurite:3.37.0@sha256:830430c1da1a2d537e08f3e6764dd1f5ae00cf0346bcaf625b968ec3f0971fd5";
}
