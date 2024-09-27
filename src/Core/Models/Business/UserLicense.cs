using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json.Serialization;
using Bit.Core.Billing.Attributes;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Microsoft.IdentityModel.Tokens;

namespace Bit.Core.Models.Business;

public class UserLicense : ILicense
{
    /// <summary>
    /// Represents the current version of the license format. Previously, the engineer who adds a property to this class
    /// would need to increment this constant. Now, if you add a property, you must simply add a new claim
    /// </summary>
    /// <remarks>Intentionally set one version behind to allow self-hosted users some time to update before
    /// getting out of date license errors</remarks>
    public const int CurrentLicenseFileVersion = 0;

    public UserLicense()
    { }

    public UserLicense(
        User user,
        SubscriptionInfo subscriptionInfo,
        ILicensingService licenseService,
        int? version = null)
    {
        LicenseType = Core.Enums.LicenseType.User;
        LicenseKey = user.LicenseKey;
        Id = user.Id;
        Name = user.Name;
        Email = user.Email;
        Version = version.GetValueOrDefault(1);
        Premium = user.Premium;
        MaxStorageGb = user.MaxStorageGb;
        Issued = DateTime.UtcNow;
        Expires = subscriptionInfo?.UpcomingInvoice?.Date?.AddDays(7) ??
            user.PremiumExpirationDate?.AddDays(7);
        Refresh = subscriptionInfo?.UpcomingInvoice?.Date;
        Trial = (subscriptionInfo?.Subscription?.TrialEndDate.HasValue ?? false) &&
            subscriptionInfo.Subscription.TrialEndDate.Value > DateTime.UtcNow;

        Hash = Convert.ToBase64String(EncodedHash); // Hash must come after all properties are set, and before Signature
        Signature = Convert.ToBase64String(licenseService.SignLicense(this));
        Token = licenseService.GenerateToken(this);
    }

    public UserLicense(User user, ILicensingService licenseService, int? version = null)
    {
        LicenseType = Core.Enums.LicenseType.User;
        LicenseKey = user.LicenseKey;
        Id = user.Id;
        Name = user.Name;
        Email = user.Email;
        Version = version.GetValueOrDefault(1);
        Premium = user.Premium;
        MaxStorageGb = user.MaxStorageGb;
        Issued = DateTime.UtcNow;
        Expires = user.PremiumExpirationDate?.AddDays(7);
        Refresh = user.PremiumExpirationDate?.Date;
        Trial = false;

        Hash = Convert.ToBase64String(EncodedHash); // Hash must come after all properties are set, and before Signature
        Signature = Convert.ToBase64String(licenseService.SignLicense(this));
        Token = licenseService.GenerateToken(this);
    }

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
    public LicenseType? LicenseType { get; set; }

    [LicenseIgnore]
    public string Signature { get; set; }

    [LicenseIgnore]
    public string Token { get; set; }

    [JsonIgnore]
    [LicenseIgnore]
    public byte[] SignatureBytes => Convert.FromBase64String(Signature);


    [LicenseIgnore]
    [JsonIgnore]
    public byte[] EncodedData => ComputeEncodedData(p => ShouldIncludeProperty(p));

    [LicenseIgnore]
    [JsonIgnore]
    public byte[] EncodedHash => SHA256.HashData(
        ComputeEncodedData(
            p => ShouldIncludeProperty(p, LicenseIgnoreCondition.OnHash)));

    private bool ValidLicenseVersion => Version is >= 1 and <= CurrentLicenseFileVersion + 1;

    public bool CanUse(User user, out string exception)
    {
        var errorMessages = new StringBuilder();

        if (Issued > DateTime.UtcNow)
        {
            errorMessages.AppendLine("The license hasn't been issued yet.");
        }

        if (Expires < DateTime.UtcNow)
        {
            errorMessages.AppendLine("The license has expired.");
        }

        if (Version != 1)
        {
            throw new NotSupportedException($"Version {Version} is not supported.");
        }

        if (!user.EmailVerified)
        {
            errorMessages.AppendLine("The user's email is not verified.");
        }

        if (!user.Email.Equals(Email, StringComparison.InvariantCultureIgnoreCase))
        {
            errorMessages.AppendLine("The user's email does not match the license email.");
        }

        if (errorMessages.Length > 0)
        {
            exception = $"Invalid license. {errorMessages.ToString().TrimEnd()}";
            return false;
        }

        exception = "";
        return true;
    }

    public bool VerifyData(User user)
    {
        if (Issued > DateTime.UtcNow || Expires < DateTime.UtcNow)
        {
            return false;
        }

        if (Version == 1)
        {
            return
                user.LicenseKey != null && user.LicenseKey.Equals(LicenseKey) &&
                user.Premium == Premium &&
                user.Email.Equals(Email, StringComparison.InvariantCultureIgnoreCase);
        }

        throw new NotSupportedException($"Version {Version} is not supported.");
    }

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
                new Claim(nameof(LicenseType), LicenseType?.ToString() ?? string.Empty)
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

    /// <summary>
    /// Converts the license to an encoded byte array (string)
    /// </summary>
    /// <param name="shouldIncludeProperty"></param>
    /// <returns></returns>
    /// <exception cref="NotSupportedException"></exception>
    private byte[] ComputeEncodedData(Func<PropertyInfo, bool> shouldIncludeProperty)
    {
        if (!ValidLicenseVersion)
        {
            throw new NotSupportedException($"Version {Version} is not supported.");
        }

        var props = typeof(OrganizationLicense)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(shouldIncludeProperty)
            .OrderBy(p => p.Name)
            .Select(p => $"{p.Name}:{CoreHelpers.FormatLicenseSignatureValue(p.GetValue(this, null))}")
            .Aggregate((c, n) => $"{c}|{n}");

        var data = $"license:user|{props}";

        return Encoding.UTF8.GetBytes(data);
    }

    /// <summary>
    /// Determines whether a property should be included when encoding the license data
    /// </summary>
    /// <param name="p"></param>
    /// <param name="additionalCondition"></param>
    /// <returns></returns>
    private bool ShouldIncludeProperty(
        PropertyInfo p,
        LicenseIgnoreCondition additionalCondition = LicenseIgnoreCondition.Always)
    {
        var licenseIgnoreAttribute = p.GetCustomAttribute<LicenseIgnoreAttribute>();
        var shouldNotIgnore = licenseIgnoreAttribute is null || licenseIgnoreAttribute.Condition != additionalCondition;
        var versionIsSupported = (p.GetCustomAttribute<LicenseVersionAttribute>()?.Version ?? 1) <= Version;

        return shouldNotIgnore && versionIsSupported;
    }
}
