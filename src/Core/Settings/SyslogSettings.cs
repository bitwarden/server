namespace Bit.Core.Settings;
public class SyslogSettings
{
    /// <summary>
    /// The connection string used to connect to a remote syslog server over TCP or UDP, or to connect locally.
    /// </summary>
    /// <remarks>
    /// <para>The connection string will be parsed using <see cref="System.Uri" /> to extract the protocol, host name and port number.
    /// </para>
    /// <para>
    /// Supported protocols are:
    /// <list type="bullet">
    /// <item>UDP (use <code>udp://</code>)</item>
    /// <item>TCP (use <code>tcp://</code>)</item>
    /// <item>TLS over TCP (use <code>tls://</code>)</item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <example>
    /// A remote server (logging.dev.example.com) is listening on UDP (port 514):
    /// <code>
    /// udp://logging.dev.example.com:514</code>.
    /// </example>
    public string Destination { get; set; }
    /// <summary>
    /// The absolute path to a Certificate (DER or Base64 encoded with private key).
    /// </summary>
    /// <remarks>
    /// The certificate path and <see cref="CertificatePassword"/> are passed into the <see cref="System.Security.Cryptography.X509Certificates.X509Certificate2.X509Certificate2(string, string)" />.
    /// The file format of the certificate may be binary encoded (DER) or base64. If the private key is encrypted, provide the password in <see cref="CertificatePassword"/>,
    /// </remarks>
    public string CertificatePath { get; set; }
    /// <summary>
    /// The password for the encrypted private key in the certificate supplied in <see cref="CertificatePath" />.
    /// </summary>
    /// <value></value>
    public string CertificatePassword { get; set; }
    /// <summary>
    /// The thumbprint of the certificate in the X.509 certificate store for personal certificates for the user account running Bitwarden.
    /// </summary>
    /// <value></value>
    public string CertificateThumbprint { get; set; }
}

