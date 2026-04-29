// Usage: dotnet run --project dev/generate-test-license-token -- <thumbprint>
//
// Generates a serialized OrganizationLicense with a signed JWT Token field,
// suitable for testing ILicensingService.GetClaimsPrincipalFromLicense.
//
// Dev cert thumbprint:  207E64A231E8AA32AAF68A61037C075EBEBD553F
// Prod cert thumbprint: B34876439FCDA2846505B2EFBBA6C4A951313EBE

using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

var thumbprint = args.ElementAtOrDefault(0)
    ?? throw new Exception("Usage: dotnet run --project dev/generate-test-license-token -- <thumbprint>");

var cert = CoreHelpers.GetCertificate(thumbprint)
    ?? throw new Exception($"Certificate with thumbprint {thumbprint} not found in CurrentUser store.");
if (!cert.HasPrivateKey)
    throw new Exception("Certificate does not have a private key. Import a .pfx with the private key.");

Console.Error.WriteLine($"Loaded cert: {cert.Subject} (thumbprint: {cert.Thumbprint})");

var claims = new List<Claim>
{
    new(JwtRegisteredClaimNames.Jti, Guid.Empty.ToString()),
    new("test", "true"),
    new("Enabled", "false"),
};

var securityKey = new RsaSecurityKey(cert.GetRSAPrivateKey());
var tokenDescriptor = new SecurityTokenDescriptor
{
    Subject = new ClaimsIdentity(claims),
    Issuer = "bitwarden",
    Audience = $"organization:{Guid.Empty}",
    NotBefore = DateTime.UtcNow,
    Expires = new DateTime(2100, 1, 1, 0, 0, 0, DateTimeKind.Utc),
    SigningCredentials = new SigningCredentials(securityKey, SecurityAlgorithms.RsaSha256Signature),
};

var handler = new JwtSecurityTokenHandler();
var token = handler.WriteToken(handler.CreateToken(tokenDescriptor));

var license = new OrganizationLicense
{
    Id = Guid.Empty,
    Token = token,
};

Console.WriteLine(JsonSerializer.Serialize(license, JsonHelpers.Indented));
