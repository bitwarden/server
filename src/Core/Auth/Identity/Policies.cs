namespace Bit.Core.Auth.Identity;

public static class Policies
{
    /// <summary>
    /// Class holding the policies that support the Tools team
    /// </summary>
    public static class Tools
    {
        public const string Send = "Send";  // [Authorize(Policy = Policies.Send)]
    }
    // TODO: migrate other existing policies to use this class
}
