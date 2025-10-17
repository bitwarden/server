namespace Bit.Core.Auth.Identity;

public static class Policies
{
    /// <summary>
    /// Policy for managing access to the Send feature.
    /// </summary>
    public const string Send = "Send";  // [Authorize(Policy = Policies.Send)]
    public const string Application = "Application"; // [Authorize(Policy = Policies.Application)]
    public const string Web = "Web"; // [Authorize(Policy = Policies.Web)]
    public const string Push = "Push"; // [Authorize(Policy = Policies.Push)]
    public const string Licensing = "Licensing"; // [Authorize(Policy = Policies.Licensing)]
    public const string Organization = "Organization"; // [Authorize(Policy = Policies.Organization)]
    public const string Installation = "Installation"; // [Authorize(Policy = Policies.Installation)]
    public const string Secrets = "Secrets"; // [Authorize(Policy = Policies.Secrets)]
}
