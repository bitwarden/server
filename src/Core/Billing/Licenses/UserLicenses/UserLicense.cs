using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json.Serialization;
using Bit.Core.Billing.Attributes;
using Bit.Core.Billing.Extensions;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Business;
using Bit.Core.Services;
using Microsoft.IdentityModel.Tokens;

namespace Bit.Core.Billing.Licenses.UserLicenses;

public class UserLicense : ILicense
{
    /// <summary>
    /// Represents the current version of the license format. Previously, the engineer who adds a property to this class
    /// would need to increment this constant. Now, if you add a property, you must simply add a new claim
    /// </summary>
    /// <remarks>Intentionally set one version behind to allow self-hosted users some time to update before
    /// getting out of date license errors</remarks>
    public const int CurrentLicenseFileVersion = 0;

    public string LicenseKey { get; set; }

    public Guid Id { get; set; }

    public string Name { get; set; }

    public string Email { get; set; }

    public bool Premium { get; set; }

    public short? MaxStorageGb { get; set; }

    public int Version { get; set; }

    public DateTime? Expires { get; set; }

    public bool Trial { get; set; }

    [LicenseIgnore(Condition = LicenseIgnoreCondition.OnHash)]
    public DateTime Issued { get; set; }

    [LicenseIgnore(Condition = LicenseIgnoreCondition.OnHash)]
    public DateTime? Refresh { get; set; }

    [LicenseIgnore(Condition = LicenseIgnoreCondition.OnHash)]
    public string Hash { get; set; }

    [LicenseIgnore]
    public LicenseType LicenseType { get; } = LicenseType.User;

    [LicenseIgnore]
    public string Signature { get; set; }

    [LicenseIgnore]
    public string Token { get; set; }

    [JsonIgnore]
    [LicenseIgnore]
    public byte[] SignatureBytes => Convert.FromBase64String(Signature);

    [LicenseIgnore]
    [JsonIgnore]
    public byte[] EncodedData => this.EncodeLicense(p => p.ShouldIncludePropertyOnLicense(Version));

    [LicenseIgnore]
    [JsonIgnore]
    public byte[] EncodedHash =>
        SHA256.HashData(
            this.EncodeLicense(p => p.ShouldIncludePropertyOnLicense(Version, LicenseIgnoreCondition.OnHash)));

    public bool ValidLicenseVersion => Version is >= 1 and <= CurrentLicenseFileVersion + 1;

    public string ToToken(X509Certificate2 certificate)
    {
        if (!certificate.HasPrivateKey)
        {
            throw new InvalidOperationException("You don't have the private key!");
        }

        using var rsa = certificate.GetRSAPrivateKey();

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([
                new Claim(nameof(LicenseKey), LicenseKey),
                new Claim(nameof(Id), Id.ToString()),
                new Claim(nameof(Name), Name),
                new Claim(nameof(Email), Email),
                new Claim(nameof(Premium), Premium.ToString()),
                new Claim(nameof(MaxStorageGb), MaxStorageGb.ToString()),
                new Claim(nameof(Version), Version.ToString()),
                new Claim(nameof(Issued), Issued.ToString()),
                new Claim(nameof(Refresh), Refresh.ToString()),
                new Claim(nameof(Expires), Expires.ToString()),
                new Claim(nameof(Trial), Trial.ToString()),
                new Claim(nameof(LicenseType), LicenseType.ToString())
            ]),
            Issuer = "Bitwarden",
            Audience = Id.ToString(),
            NotBefore = Issued,
            Expires = Issued.AddDays(7),
            SigningCredentials = new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256)
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateJwtSecurityToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}
