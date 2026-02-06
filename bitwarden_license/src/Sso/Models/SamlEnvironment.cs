// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.Security.Cryptography.X509Certificates;

namespace Bit.Sso.Models;

public class SamlEnvironment
{
    public X509Certificate2 SpSigningCertificate { get; set; }
}
