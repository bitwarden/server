namespace Bit.Core.KeyManagement.Sends;

// This should not be used except for DI as open generic marker class for use with
// the SendPasswordHasher.
public class SendPasswordHasherMarker
{
    // We know we will pass a single instance that isn't used to the PasswordHasher so we
    // gain an efficiency benefit of not creating multiple marker classes.
    public static readonly SendPasswordHasherMarker Instance = new();
}
