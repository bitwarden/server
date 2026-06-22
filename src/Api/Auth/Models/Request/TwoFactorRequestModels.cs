// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.ComponentModel.DataAnnotations;
using Bit.Api.Auth.Models.Request.Accounts;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Identity.TokenProviders;
using Bit.Core.Auth.Models;
using Bit.Core.Entities;
using Fido2NetLib;

namespace Bit.Api.Auth.Models.Request;

/// <summary>Request body for <c>PUT /two-factor/authenticator</c>.</summary>
public class TwoFactorAuthenticatorUpdateRequestModel
{
    /// <summary>Six-digit TOTP code from the authenticator app, proving the user enrolled <see cref="Key"/>.</summary>
    [Required]
    [StringLength(50)]
    public string Token { get; set; }

    /// <summary>TOTP shared secret that the token was minted against; must match the token's bound Key.</summary>
    [Required]
    [StringLength(50)]
    public string Key { get; set; }

    /// <summary>Token minted by <c>GetAuthenticator</c>; bound to <c>UserId + Key</c>.</summary>
    [Required]
    public string UserVerificationToken { get; set; }

    public User ToUser(User existingUser)
    {
        var providers = existingUser.GetTwoFactorProviders();
        if (providers == null)
        {
            providers = new Dictionary<TwoFactorProviderType, TwoFactorProvider>();
        }
        else
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

public class TwoFactorDuoUpdateRequestModel : IValidatableObject
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
    [Required]
    public string UserVerificationToken { get; set; }

    public User ToUser(User existingUser)
    {
        var providers = existingUser.GetTwoFactorProviders();
        if (providers == null)
        {
            providers = [];
        }
        else
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
        else
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

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
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

public class TwoFactorYubiKeyUpdateRequestModel : IValidatableObject
{
    public string Key1 { get; set; }
    public string Key2 { get; set; }
    public string Key3 { get; set; }
    public string Key4 { get; set; }
    public string Key5 { get; set; }
    [Required]
    public bool? Nfc { get; set; }
    [Required]
    public string UserVerificationToken { get; set; }

    public User ToUser(User existingUser)
    {
        var providers = existingUser.GetTwoFactorProviders();
        if (providers == null)
        {
            providers = new Dictionary<TwoFactorProviderType, TwoFactorProvider>();
        }
        else
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

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
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

/// <summary>
/// Request body for the anonymous login-time endpoint that emails a 2FA OTP during sign-in. Authenticated
/// by master password / OTP, SSO email-2FA session token, or device-auth-request access code.
/// </summary>
public class TwoFactorEmailLoginRequestModel : SecretVerificationRequestModel
{
    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; }
    public string AuthRequestId { get; set; }
    // An auth session token used for obtaining email and as an authN factor for the sending of emailed 2FA OTPs.
    public string SsoEmail2FaSessionToken { get; set; }

    public override IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrEmpty(Secret)
            && string.IsNullOrEmpty(AuthRequestAccessCode)
            && string.IsNullOrEmpty(SsoEmail2FaSessionToken))
        {
            yield return new ValidationResult("MasterPasswordHash, OTP, AccessCode, or SsoEmail2faSessionToken must be supplied.");
        }
    }
}

public class TwoFactorWebAuthnUpdateRequestModel : TwoFactorWebAuthnDeleteRequestModel
{
    [Required]
    public AuthenticatorAttestationRawResponse DeviceResponse { get; set; }
    public string Name { get; set; }
}

public class TwoFactorWebAuthnDeleteRequestModel : IValidatableObject
{
    [Required]
    public int? Id { get; set; }
    [Required]
    public string UserVerificationToken { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Id.HasValue)
        {
            yield return new ValidationResult("Invalid Key Id", new string[] { nameof(Id) });
        }
    }
}

/// <summary>
/// Request body for the authenticated setup endpoint that sends a verification OTP to the user's chosen
/// 2FA email address. Authenticated by a user-verification token minted earlier in the setup flow.
/// </summary>
public class TwoFactorEmailSetupRequestModel
{
    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; }

    [Required]
    public string UserVerificationToken { get; set; }

    public User ToUser(User existingUser)
    {
        var providers = existingUser.GetTwoFactorProviders();
        if (providers == null)
        {
            providers = new Dictionary<TwoFactorProviderType, TwoFactorProvider>();
        }
        else
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
}

/// <summary>
/// Request body for the authenticated setup endpoint that completes Email 2FA enrollment by replaying the
/// OTP from the previous setup step. Authenticated by the same user-verification token.
/// </summary>
public class TwoFactorEmailUpdateRequestModel : TwoFactorEmailSetupRequestModel
{
    [Required]
    [StringLength(50)]
    public string Token { get; set; }
}

/// <summary>Request body for <c>DELETE /two-factor/authenticator</c>.</summary>
public class TwoFactorAuthenticatorDeleteRequestModel
{
    /// <summary>Token minted by <c>GetAuthenticator</c>; bound to <c>UserId + Key</c>.</summary>
    [Required]
    public string UserVerificationToken { get; set; }

    /// <summary>TOTP shared secret that the token was minted against; must match the token's bound Key.</summary>
    [Required]
    public string Key { get; set; }
}
