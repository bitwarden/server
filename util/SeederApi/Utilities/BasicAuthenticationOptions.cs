using Microsoft.AspNetCore.Authentication;

namespace Bit.SeederApi.Utilities;

public class BasicAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string DefaultScheme = "SeederBasicAuth";
}
