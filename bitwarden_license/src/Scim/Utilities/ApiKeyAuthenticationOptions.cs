using Microsoft.AspNetCore.Authentication;

namespace Bit.Scim.Utilities;

public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string DefaultScheme = "ScimApiKey";
}
