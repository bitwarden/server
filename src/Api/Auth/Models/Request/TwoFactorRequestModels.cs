using System.ComponentModel.DataAnnotations;
using Bit.Api.Auth.Models.Request.Accounts;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Identity.TokenProviders;
using Bit.Core.Auth.Models;
using Bit.Core.Entities;
using Fido2NetLib;

namespace Bit.Api.Auth.Models.Request;

public class UpdateTwoFactorAuthenticatorRequestModel : SecretVerificationRequestModel
{
    [Required]
    [StringLength(50)]
    public string Token { get; set; }
    [Required]
    [StringLength(50)]
    public string Key { get; set; }
    public string UserVerificationToken { get; set; }
    public User ToUser(User existingUser)
    {
        var providers = existingUser.GetTwoFactorProviders();
        if (providers == null)
        {
            providers = new Dictionary<TwoFactorProviderType, TwoFactorProvider>();
        }
        else if (providers.ContainsKey(TwoFactorProviderType.Authenticator))
        {
            providers.Remove(TwoFactorProviderType.Authenticator);
        }

        providers.Add(TwoFactorProviderType.Authenticator, new TwoFactorProvider
        {
            MetaData = new Dictionary<string, object> { ["Key"] = Key },
            Enabled = true
        });
        existingUser.SetTwoFactorProviders(providers);
        return existingUser;
    }
}

public class UpdateTwoFactorDuoRequestModel : SecretVerificationRequestModel, IValidatableObject
{
    /*
        String lengths based on Duo's documentation
        https://github.com/duosecurity/duo_universal_csharp/blob/main/DuoUniversal/Client.cs
    */
    [Required]
    [StringLength(20, MinimumLength = 20, ErrorMessage = "Client Id must be exactly 20 characters.")]
    public string ClientId { get; set; }
    [Required]
    [StringLength(40, MinimumLength = 40, ErrorMessage = "Client Secret must be exactly 40 characters.")]
    public string ClientSecret { get; set; }
    [Required]
    public string Host { get; set; }

    public User ToUser(User existingUser)
    {
        var providers = existingUser.GetTwoFactorProviders();
        if (providers == null)
        {
            providers = [];
        }
        else if (providers.ContainsKey(TwoFactorProviderType.Duo))
        {
            providers.Remove(TwoFactorProviderType.Duo);
        }

        providers.Add(TwoFactorProviderType.Duo, new TwoFactorProvider
        {
            MetaData = new Dictionary<string, object>
            {
                ["ClientSecret"] = ClientSecret,
                ["ClientId"] = ClientId,
                ["Host"] = Host
            },
            Enabled = true
        });
        existingUser.SetTwoFactorProviders(providers);
        return existingUser;
    }

    public Organization ToOrganization(Organization existingOrg)
    {
        var providers = existingOrg.GetTwoFactorProviders();
        if (providers == null)
        {
            providers = [];
        }
        else if (providers.ContainsKey(TwoFactorProviderType.OrganizationDuo))
        {
            providers.Remove(TwoFactorProviderType.OrganizationDuo);
        }

        providers.Add(TwoFactorProviderType.OrganizationDuo, new TwoFactorProvider
        {
            MetaData = new Dictionary<string, object>
            {
                ["ClientSecret"] = ClientSecret,
                ["ClientId"] = ClientId,
                ["Host"] = Host
            },
            Enabled = true
        });
        existingOrg.SetTwoFactorProviders(providers);
        return existingOrg;
    }

    public override IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var results = new List<ValidationResult>();
        if (string.IsNullOrWhiteSpace(ClientId))
        {
            results.Add(new ValidationResult("ClientId is required.", [nameof(ClientId)]));
        }

        if (string.IsNullOrWhiteSpace(ClientSecret))
        {
            results.Add(new ValidationResult("ClientSecret is required.", [nameof(ClientSecret)]));
        }

        if (string.IsNullOrWhiteSpace(Host) || !DuoUniversalTokenService.ValidDuoHost(Host))
        {
            results.Add(new ValidationResult("Host is invalid.", [nameof(Host)]));
        }
        return results;
    }
}

public class UpdateTwoFactorYubicoOtpRequestModel : SecretVerificationRequestModel, IValidatableObject
{
    public string Key1 { get; set; }
    public string Key2 { get; set; }
    public string Key3 { get; set; }
    public string Key4 { get; set; }
    public string Key5 { get; set; }
    [Required]
    public bool? Nfc { get; set; }

    public User ToUser(User existingUser)
    {
        var providers = existingUser.GetTwoFactorProviders();
        if (providers == null)
        {
            providers = new Dictionary<TwoFactorProviderType, TwoFactorProvider>();
        }
        else if (providers.ContainsKey(TwoFactorProviderType.YubiKey))
        {
            providers.Remove(TwoFactorProviderType.YubiKey);
        }

        providers.Add(TwoFactorProviderType.YubiKey, new TwoFactorProvider
        {
            MetaData = new Dictionary<string, object>
            {
                ["Key1"] = FormatKey(Key1),
                ["Key2"] = FormatKey(Key2),
                ["Key3"] = FormatKey(Key3),
                ["Key4"] = FormatKey(Key4),
                ["Key5"] = FormatKey(Key5),
                ["Nfc"] = Nfc.Value
            },
            Enabled = true
        });
        existingUser.SetTwoFactorProviders(providers);
        return existingUser;
    }

    private string FormatKey(string keyValue)
    {
        if (string.IsNullOrWhiteSpace(keyValue))
        {
            return null;
        }

        return keyValue.Substring(0, 12);
    }

    public override IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Key1) && string.IsNullOrWhiteSpace(Key2) && string.IsNullOrWhiteSpace(Key3) &&
            string.IsNullOrWhiteSpace(Key4) && string.IsNullOrWhiteSpace(Key5))
        {
            yield return new ValidationResult("A key is required.", new string[] { nameof(Key1) });
        }

        if (!string.IsNullOrWhiteSpace(Key1) && Key1.Length < 12)
        {
            yield return new ValidationResult("Key 1 in invalid.", new string[] { nameof(Key1) });
        }

        if (!string.IsNullOrWhiteSpace(Key2) && Key2.Length < 12)
        {
            yield return new ValidationResult("Key 2 in invalid.", new string[] { nameof(Key2) });
        }

        if (!string.IsNullOrWhiteSpace(Key3) && Key3.Length < 12)
        {
            yield return new ValidationResult("Key 3 in invalid.", new string[] { nameof(Key3) });
        }

        if (!string.IsNullOrWhiteSpace(Key4) && Key4.Length < 12)
        {
            yield return new ValidationResult("Key 4 in invalid.", new string[] { nameof(Key4) });
        }

        if (!string.IsNullOrWhiteSpace(Key5) && Key5.Length < 12)
        {
            yield return new ValidationResult("Key 5 in invalid.", new string[] { nameof(Key5) });
        }
    }
}

public class TwoFactorEmailRequestModel : SecretVerificationRequestModel
{
    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; }
    public string AuthRequestId { get; set; }
    // An auth session token used for obtaining email and as an authN factor for the sending of emailed 2FA OTPs.
    public string SsoEmail2FaSessionToken { get; set; }
    public User ToUser(User existingUser)
    {
        var providers = existingUser.GetTwoFactorProviders();
        if (providers == null)
        {
            providers = new Dictionary<TwoFactorProviderType, TwoFactorProvider>();
        }
        else if (providers.ContainsKey(TwoFactorProviderType.Email))
        {
            providers.Remove(TwoFactorProviderType.Email);
        }

        providers.Add(TwoFactorProviderType.Email, new TwoFactorProvider
        {
            MetaData = new Dictionary<string, object> { ["Email"] = Email.ToLowerInvariant() },
            Enabled = true
        });
        existingUser.SetTwoFactorProviders(providers);
        return existingUser;
    }

    public override IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrEmpty(Secret) && string.IsNullOrEmpty(AuthRequestAccessCode) && string.IsNullOrEmpty((SsoEmail2FaSessionToken)))
        {
            yield return new ValidationResult("MasterPasswordHash, OTP, AccessCode, or SsoEmail2faSessionToken must be supplied.");
        }
    }
}

public class TwoFactorWebAuthnRequestModel : TwoFactorWebAuthnDeleteRequestModel
{
    [Required]
    public AuthenticatorAttestationRawResponse DeviceResponse { get; set; }
    public string Name { get; set; }
}

public class TwoFactorWebAuthnDeleteRequestModel : SecretVerificationRequestModel, IValidatableObject
{
    [Required]
    public int? Id { get; set; }

    public override IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (var validationResult in base.Validate(validationContext))
        {
            yield return validationResult;
        }

        if (!Id.HasValue || Id < 0 || Id > 5)
        {
            yield return new ValidationResult("Invalid Key Id", new string[] { nameof(Id) });
        }
    }
}

public class UpdateTwoFactorEmailRequestModel : TwoFactorEmailRequestModel
{
    [Required]
    [StringLength(50)]
    public string Token { get; set; }
}

public class TwoFactorProviderRequestModel : SecretVerificationRequestModel
{
    [Required]
    public TwoFactorProviderType? Type { get; set; }
}

public class TwoFactorRecoveryRequestModel : TwoFactorEmailRequestModel
{
    [Required]
    [StringLength(32)]
    public string RecoveryCode { get; set; }
}

public class TwoFactorAuthenticatorDisableRequestModel : TwoFactorProviderRequestModel
{
    [Required]
    public string UserVerificationToken { get; set; }
    [Required]
    public string Key { get; set; }
}
