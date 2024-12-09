using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json.Serialization;
using Bit.Core.Billing.Licenses.Extensions;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Services;

namespace Bit.Core.Models.Business;

public class UserLicense : ILicense
{
    public UserLicense()
    { }

    public UserLicense(User user, SubscriptionInfo subscriptionInfo, ILicensingService licenseService,
        int? version = null)
    {
        LicenseType = Enums.LicenseType.User;
        LicenseKey = user.LicenseKey;
        Id = user.Id;
        Name = user.Name;
        Email = user.Email;
        Version = version.GetValueOrDefault(1);
        Premium = user.Premium;
        MaxStorageGb = user.MaxStorageGb;
        Issued = DateTime.UtcNow;
        Expires = subscriptionInfo?.UpcomingInvoice?.Date != null ?
            subscriptionInfo.UpcomingInvoice.Date.Value.AddDays(7) :
            user.PremiumExpirationDate?.AddDays(7);
        Refresh = subscriptionInfo?.UpcomingInvoice?.Date;
        Trial = (subscriptionInfo?.Subscription?.TrialEndDate.HasValue ?? false) &&
            subscriptionInfo.Subscription.TrialEndDate.Value > DateTime.UtcNow;

        Hash = Convert.ToBase64String(ComputeHash());
        Signature = Convert.ToBase64String(licenseService.SignLicense(this));
    }

    public UserLicense(User user, ILicensingService licenseService, int? version = null)
    {
        LicenseType = Enums.LicenseType.User;
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

        Hash = Convert.ToBase64String(ComputeHash());
        Signature = Convert.ToBase64String(licenseService.SignLicense(this));
    }

    public string LicenseKey { get; set; }
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public bool Premium { get; set; }
    public short? MaxStorageGb { get; set; }
    public int Version { get; set; }
    public DateTime Issued { get; set; }
    public DateTime? Refresh { get; set; }
    public DateTime? Expires { get; set; }
    public bool Trial { get; set; }
    public LicenseType? LicenseType { get; set; }
    public string Hash { get; set; }
    public string Signature { get; set; }
    public string Token { get; set; }
    [JsonIgnore]
    public byte[] SignatureBytes => Convert.FromBase64String(Signature);

    public byte[] GetDataBytes(bool forHash = false)
    {
        string data = null;
        if (Version == 1)
        {
            var props = typeof(UserLicense)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p =>
                    !p.Name.Equals(nameof(Signature)) &&
                    !p.Name.Equals(nameof(SignatureBytes)) &&
                    !p.Name.Equals(nameof(LicenseType)) &&
                    !p.Name.Equals(nameof(Token)) &&
                    (
                        !forHash ||
                        (
                            !p.Name.Equals(nameof(Hash)) &&
                            !p.Name.Equals(nameof(Issued)) &&
                            !p.Name.Equals(nameof(Refresh))
                        )
                    ))
                .OrderBy(p => p.Name)
                .Select(p => $"{p.Name}:{Utilities.CoreHelpers.FormatLicenseSignatureValue(p.GetValue(this, null))}")
                .Aggregate((c, n) => $"{c}|{n}");
            data = $"license:user|{props}";
        }
        else
        {
            throw new NotSupportedException($"Version {Version} is not supported.");
        }

        return Encoding.UTF8.GetBytes(data);
    }

    public byte[] ComputeHash()
    {
        using (var alg = SHA256.Create())
        {
            return alg.ComputeHash(GetDataBytes(true));
        }
    }

    public bool CanUse(User user, ClaimsPrincipal claimsPrincipal, out string exception)
    {
        if (string.IsNullOrWhiteSpace(Token) || claimsPrincipal is null)
        {
            return ObsoleteCanUse(user, out exception);
        }

        var errorMessages = new StringBuilder();

        if (!user.EmailVerified)
        {
            errorMessages.AppendLine("The user's email is not verified.");
        }

        var email = claimsPrincipal.GetValue<string>(nameof(Email));
        if (!email.Equals(user.Email, StringComparison.InvariantCultureIgnoreCase))
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

    /// <summary>
    /// Do not extend this method. It is only here for backwards compatibility with old licenses.
    /// Instead, extend the CanUse method using the ClaimsPrincipal.
    /// </summary>
    /// <param name="user"></param>
    /// <param name="exception"></param>
    /// <returns></returns>
    /// <exception cref="NotSupportedException"></exception>
    private bool ObsoleteCanUse(User user, out string exception)
    {
        // Do not extend this method. It is only here for backwards compatibility with old licenses.
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

    public bool VerifyData(User user, ClaimsPrincipal claimsPrincipal)
    {
        if (string.IsNullOrWhiteSpace(Token) || claimsPrincipal is null)
        {
            return ObsoleteVerifyData(user);
        }

        var licenseKey = claimsPrincipal.GetValue<string>(nameof(LicenseKey));
        var premium = claimsPrincipal.GetValue<bool>(nameof(Premium));
        var email = claimsPrincipal.GetValue<string>(nameof(Email));

        return licenseKey == user.LicenseKey &&
               premium == user.Premium &&
               email.Equals(user.Email, StringComparison.InvariantCultureIgnoreCase);
    }

    /// <summary>
    /// Do not extend this method. It is only here for backwards compatibility with old licenses.
    /// Instead, extend the VerifyData method using the ClaimsPrincipal.
    /// </summary>
    /// <param name="user"></param>
    /// <returns></returns>
    /// <exception cref="NotSupportedException"></exception>
    private bool ObsoleteVerifyData(User user)
    {
        // Do not extend this method. It is only here for backwards compatibility with old licenses.
        if (Issued > DateTime.UtcNow || Expires < DateTime.UtcNow)
        {
            return false;
        }

        if (Version != 1)
        {
            throw new NotSupportedException($"Version {Version} is not supported.");
        }

        return
            user.LicenseKey != null && user.LicenseKey.Equals(LicenseKey) &&
            user.Premium == Premium &&
            user.Email.Equals(Email, StringComparison.InvariantCultureIgnoreCase);
    }

    public bool VerifySignature(X509Certificate2 certificate)
    {
        using (var rsa = certificate.GetRSAPublicKey())
        {
            return rsa.VerifyData(GetDataBytes(), SignatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
    }

    public byte[] Sign(X509Certificate2 certificate)
    {
        if (!certificate.HasPrivateKey)
        {
            throw new InvalidOperationException("You don't have the private key!");
        }

        using (var rsa = certificate.GetRSAPrivateKey())
        {
            return rsa.SignData(GetDataBytes(), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
    }
}
