using System;
using Bit.Core.Enums;
using Bit.Core.Utilities;
using System.Collections.Generic;
using Newtonsoft.Json;
using Bit.Core.Services;
using Bit.Core.Exceptions;
using Microsoft.AspNetCore.Identity;

namespace Bit.Core.Models.Table
{
    public class User : ITableObject<Guid>, ISubscriber, IStorable, IStorableSubscriber, IRevisable, ITwoFactorProvidersUser
    {
        private Dictionary<TwoFactorProviderType, TwoFactorProvider> _twoFactorProviders;

        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public bool EmailVerified { get; set; }
        public string MasterPassword { get; set; }
        public string MasterPasswordHint { get; set; }
        public string Culture { get; set; } = "en-US";
        public string SecurityStamp { get; set; }
        public string TwoFactorProviders { get; set; }
        public string TwoFactorRecoveryCode { get; set; }
        public string EquivalentDomains { get; set; }
        public string ExcludedGlobalEquivalentDomains { get; set; }
        public DateTime AccountRevisionDate { get; internal set; } = DateTime.UtcNow;
        public string Key { get; set; }
        public string PublicKey { get; set; }
        public string PrivateKey { get; set; }
        public bool Premium { get; set; }
        public DateTime? PremiumExpirationDate { get; set; }
        public DateTime? RenewalReminderDate { get; set; }
        public long? Storage { get; set; }
        public short? MaxStorageGb { get; set; }
        public GatewayType? Gateway { get; set; }
        public string GatewayCustomerId { get; set; }
        public string GatewaySubscriptionId { get; set; }
        public string LicenseKey { get; set; }
        public KdfType Kdf { get; set; } = KdfType.PBKDF2_SHA256;
        public int KdfIterations { get; set; } = 5000;
        public DateTime CreationDate { get; internal set; } = DateTime.UtcNow;
        public DateTime RevisionDate { get; internal set; } = DateTime.UtcNow;

        public void SetNewId()
        {
            Id = CoreHelpers.GenerateComb();
        }

        public string BillingEmailAddress()
        {
            return Email;
        }

        public string BillingName()
        {
            return Name;
        }

        public string BraintreeCustomerIdPrefix()
        {
            return "u";
        }

        public string BraintreeIdField()
        {
            return "user_id";
        }

        public string GatewayIdField()
        {
            return "userId";
        }

        public Dictionary<TwoFactorProviderType, TwoFactorProvider> GetTwoFactorProviders()
        {
            if(string.IsNullOrWhiteSpace(TwoFactorProviders))
            {
                return null;
            }

            try
            {
                if(_twoFactorProviders == null)
                {
                    _twoFactorProviders =
                        JsonConvert.DeserializeObject<Dictionary<TwoFactorProviderType, TwoFactorProvider>>(
                            TwoFactorProviders);
                }

                return _twoFactorProviders;
            }
            catch(JsonSerializationException)
            {
                return null;
            }
        }

        public Guid? GetUserId()
        {
            return Id;
        }

        public bool GetPremium()
        {
            return Premium;
        }

        public void SetTwoFactorProviders(Dictionary<TwoFactorProviderType, TwoFactorProvider> providers)
        {
            TwoFactorProviders = JsonConvert.SerializeObject(providers, new JsonSerializerSettings
            {
                ContractResolver = new EnumKeyResolver<byte>()
            });
            _twoFactorProviders = providers;
        }

        public TwoFactorProvider GetTwoFactorProvider(TwoFactorProviderType provider)
        {
            var providers = GetTwoFactorProviders();
            if(providers == null || !providers.ContainsKey(provider))
            {
                return null;
            }

            return providers[provider];
        }

        public long StorageBytesRemaining()
        {
            if(!MaxStorageGb.HasValue)
            {
                return 0;
            }

            return StorageBytesRemaining(MaxStorageGb.Value);
        }

        public long StorageBytesRemaining(short maxStorageGb)
        {
            var maxStorageBytes = maxStorageGb * 1073741824L;
            if(!Storage.HasValue)
            {
                return maxStorageBytes;
            }

            return maxStorageBytes - Storage.Value;
        }

        public IdentityUser ToIdentityUser(bool twoFactorEnabled)
        {
            return new IdentityUser
            {
                Id = Id.ToString(),
                Email = Email,
                NormalizedEmail = Email,
                EmailConfirmed = EmailVerified,
                UserName = Email,
                NormalizedUserName = Email,
                TwoFactorEnabled = twoFactorEnabled,
                SecurityStamp = SecurityStamp
            };
        }
    }
}
