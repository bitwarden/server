// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Sustainsys.Saml2.Configuration;

namespace Bit.Sso.Models;

public class SamlEnvironment
{
    public ServiceCertificateCollection ServiceCertificates { get; set; } = new();
}
