using System;
using Bit.Core.Utilities;
using Bit.Core.Enums;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Linq;

namespace Bit.Core.Models.Table
{
    public class Organization : ITableObject<Guid>, ISubscriber, IStorable, IStorableSubscriber, IRevisable
    {
        private Dictionary<TwoFactorProviderType, TwoFactorProvider> _twoFactorProviders;

        public Guid Id { get; set; }
        public string Name { get; set; }
        public string BusinessName { get; set; }
        public string BusinessAddress1 { get; set; }
        public string BusinessAddress2 { get; set; }
        public string BusinessAddress3 { get; set; }
        public string BusinessCountry { get; set; }
        public string BusinessTaxNumber { get; set; }
        public string BillingEmail { get; set; }
        public string Plan { get; set; }
        public PlanType PlanType { get; set; }
        public short? Seats { get; set; }
        public short? MaxCollections { get; set; }
        public bool UseGroups { get; set; }
        public bool UseDirectory { get; set; }
        public bool UseEvents { get; set; }
        public bool UseTotp { get; set; }
        public bool Use2fa { get; set; }
        public bool UseApi { get; set; }
        public bool SelfHost { get; set; }
        public bool UsersGetPremium { get; set; }
        public long? Storage { get; set; }
        public short? MaxStorageGb { get; set; }
        public GatewayType? Gateway { get; set; }
        public string GatewayCustomerId { get; set; }
        public string GatewaySubscriptionId { get; set; }
        public bool Enabled { get; set; } = true;
        public string LicenseKey { get; set; }
        public string ApiKey { get; set; }
        public string TwoFactorProviders { get; set; }
        public DateTime? ExpirationDate { get; set; }
        public DateTime CreationDate { get; internal set; } = DateTime.UtcNow;
        public DateTime RevisionDate { get; internal set; } = DateTime.UtcNow;

        public void SetNewId()
        {
            if(Id == default(Guid))
            {
                Id = CoreHelpers.GenerateComb();
            }
        }

        public string BillingEmailAddress()
        {
            return BillingEmail;
        }

        public string BillingName()
        {
            return BusinessName;
        }

        public string BraintreeCustomerIdPrefix()
        {
            return "o";
        }

        public string BraintreeIdField()
        {
            return "organization_id";
        }

        public string GatewayIdField()
        {
            return "organizationId";
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

        public void SetTwoFactorProviders(Dictionary<TwoFactorProviderType, TwoFactorProvider> providers)
        {
            if(!providers.Any())
            {
                TwoFactorProviders = null;
                _twoFactorProviders = null;
                return;
            }

            TwoFactorProviders = JsonConvert.SerializeObject(providers, new JsonSerializerSettings
            {
                ContractResolver = new EnumKeyResolver<byte>()
            });
            _twoFactorProviders = providers;
        }

        public bool TwoFactorProviderIsEnabled(TwoFactorProviderType provider)
        {
            var providers = GetTwoFactorProviders();
            if(providers == null || !providers.ContainsKey(provider))
            {
                return false;
            }

            return providers[provider].Enabled && Use2fa;
        }

        public bool TwoFactorIsEnabled()
        {
            var providers = GetTwoFactorProviders();
            if(providers == null)
            {
                return false;
            }

            return providers.Any(p => (p.Value?.Enabled ?? false) && Use2fa);
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
    }
}
