using Bit.Core.Enums;
using Microsoft.AspNetCore.Authentication;

namespace Bit.Sso.Utilities;

public interface IDynamicAuthenticationScheme
{
    AuthenticationSchemeOptions Options { get; set; }
    SsoType SsoType { get; set; }

    Task Validate();
}
