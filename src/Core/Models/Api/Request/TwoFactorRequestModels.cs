using Bit.Core.Enums;
using Bit.Core.Models.Table;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace Bit.Core.Models.Api
{
    public class UpdateTwoFactorAuthenticatorRequestModel : TwoFactorRequestModel
    {
        [Required]
        [StringLength(50)]
        public string Token { get; set; }
        [Required]
        [StringLength(50)]
        public string Key { get; set; }

        public User ToUser(User extistingUser)
        {
            var providers = extistingUser.GetTwoFactorProviders();
            if(providers == null)
            {
                providers = new Dictionary<TwoFactorProviderType, TwoFactorProvider>();
            }
            else if(providers.ContainsKey(TwoFactorProviderType.Authenticator))
            {
                providers.Remove(TwoFactorProviderType.Authenticator);
            }

            providers.Add(TwoFactorProviderType.Authenticator, new TwoFactorProvider
            {
                MetaData = new Dictionary<string, object> { ["Key"] = Key },
                Enabled = true
            });
            extistingUser.SetTwoFactorProviders(providers);
            return extistingUser;
        }
    }

    public class UpdateTwoFactorDuoRequestModel : TwoFactorRequestModel, IValidatableObject
    {
        [Required]
        [StringLength(50)]
        public string IntegrationKey { get; set; }
        [Required]
        [StringLength(50)]
        public string SecretKey { get; set; }
        [Required]
        [StringLength(50)]
        public string Host { get; set; }

        public User ToUser(User extistingUser)
        {
            var providers = extistingUser.GetTwoFactorProviders();
            if(providers == null)
            {
                providers = new Dictionary<TwoFactorProviderType, TwoFactorProvider>();
            }
            else if(providers.ContainsKey(TwoFactorProviderType.Duo))
            {
                providers.Remove(TwoFactorProviderType.Duo);
            }

            providers.Add(TwoFactorProviderType.Duo, new TwoFactorProvider
            {
                MetaData = new Dictionary<string, object>
                {
                    ["SKey"] = SecretKey,
                    ["IKey"] = IntegrationKey,
                    ["Host"] = Host
                },
                Enabled = true
            });
            extistingUser.SetTwoFactorProviders(providers);
            return extistingUser;
        }

        public Organization ToOrganization(Organization extistingOrg)
        {
            var providers = extistingOrg.GetTwoFactorProviders();
            if(providers == null)
            {
                providers = new Dictionary<TwoFactorProviderType, TwoFactorProvider>();
            }
            else if(providers.ContainsKey(TwoFactorProviderType.OrganizationDuo))
            {
                providers.Remove(TwoFactorProviderType.OrganizationDuo);
            }

            providers.Add(TwoFactorProviderType.OrganizationDuo, new TwoFactorProvider
            {
                MetaData = new Dictionary<string, object>
                {
                    ["SKey"] = SecretKey,
                    ["IKey"] = IntegrationKey,
                    ["Host"] = Host
                },
                Enabled = true
            });
            extistingOrg.SetTwoFactorProviders(providers);
            return extistingOrg;
        }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if(!Host.StartsWith("api-") || !Host.EndsWith(".duosecurity.com"))
            {
                yield return new ValidationResult("Host is invalid.", new string[] { nameof(Host) });
            }
        }
    }

    public class UpdateTwoFactorYubicoOtpRequestModel : TwoFactorRequestModel, IValidatableObject
    {
        public string Key1 { get; set; }
        public string Key2 { get; set; }
        public string Key3 { get; set; }
        public string Key4 { get; set; }
        public string Key5 { get; set; }
        [Required]
        public bool? Nfc { get; set; }

        public User ToUser(User extistingUser)
        {
            var providers = extistingUser.GetTwoFactorProviders();
            if(providers == null)
            {
                providers = new Dictionary<TwoFactorProviderType, TwoFactorProvider>();
            }
            else if(providers.ContainsKey(TwoFactorProviderType.YubiKey))
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
            extistingUser.SetTwoFactorProviders(providers);
            return extistingUser;
        }

        private string FormatKey(string keyValue)
        {
            if(string.IsNullOrWhiteSpace(keyValue))
            {
                return null;
            }

            return keyValue.Substring(0, 12);
        }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if(string.IsNullOrWhiteSpace(Key1) && string.IsNullOrWhiteSpace(Key2) && string.IsNullOrWhiteSpace(Key3) &&
                string.IsNullOrWhiteSpace(Key4) && string.IsNullOrWhiteSpace(Key5))
            {
                yield return new ValidationResult("A key is required.", new string[] { nameof(Key1) });
            }

            if(!string.IsNullOrWhiteSpace(Key1) && Key1.Length < 12)
            {
                yield return new ValidationResult("Key 1 in invalid.", new string[] { nameof(Key1) });
            }

            if(!string.IsNullOrWhiteSpace(Key2) && Key2.Length < 12)
            {
                yield return new ValidationResult("Key 2 in invalid.", new string[] { nameof(Key2) });
            }

            if(!string.IsNullOrWhiteSpace(Key3) && Key3.Length < 12)
            {
                yield return new ValidationResult("Key 3 in invalid.", new string[] { nameof(Key3) });
            }

            if(!string.IsNullOrWhiteSpace(Key4) && Key4.Length < 12)
            {
                yield return new ValidationResult("Key 4 in invalid.", new string[] { nameof(Key4) });
            }

            if(!string.IsNullOrWhiteSpace(Key5) && Key5.Length < 12)
            {
                yield return new ValidationResult("Key 5 in invalid.", new string[] { nameof(Key5) });
            }
        }
    }

    public class TwoFactorEmailRequestModel : TwoFactorRequestModel
    {
        [Required]
        [EmailAddress]
        [StringLength(50)]
        public string Email { get; set; }

        public User ToUser(User extistingUser)
        {
            var providers = extistingUser.GetTwoFactorProviders();
            if(providers == null)
            {
                providers = new Dictionary<TwoFactorProviderType, TwoFactorProvider>();
            }
            else if(providers.ContainsKey(TwoFactorProviderType.Email))
            {
                providers.Remove(TwoFactorProviderType.Email);
            }

            providers.Add(TwoFactorProviderType.Email, new TwoFactorProvider
            {
                MetaData = new Dictionary<string, object> { ["Email"] = Email.ToLowerInvariant() },
                Enabled = true
            });
            extistingUser.SetTwoFactorProviders(providers);
            return extistingUser;
        }
    }

    public class TwoFactorU2fRequestModel : TwoFactorU2fDeleteRequestModel
    {
        [Required]
        public string DeviceResponse { get; set; }
        public string Name { get; set; }
    }

    public class TwoFactorU2fDeleteRequestModel : TwoFactorRequestModel, IValidatableObject
    {
        [Required]
        public int? Id { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if(!Id.HasValue || Id < 0 || Id > 5)
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

    public class TwoFactorProviderRequestModel : TwoFactorRequestModel
    {
        [Required]
        public TwoFactorProviderType? Type { get; set; }
    }

    public class TwoFactorRequestModel
    {
        [Required]
        public string MasterPasswordHash { get; set; }
    }

    public class TwoFactorRecoveryRequestModel : TwoFactorEmailRequestModel
    {
        [Required]
        [StringLength(32)]
        public string RecoveryCode { get; set; }
    }
}
