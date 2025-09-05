namespace Bit.Core.Auth.Identity;

public static class Policies
{
    /// <summary>
    /// Policy for managing access to the Send feature.
    /// </summary>
    public const string Send = "Send";  // [Authorize(Policy = Policies.Send)]
    // TODO: migrate other existing policies to use this class
}
