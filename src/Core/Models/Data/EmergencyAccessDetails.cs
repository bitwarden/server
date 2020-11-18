using System;
using System.Collections.Generic;
using Bit.Core.Enums;
using Bit.Core.Models.Table;
using Newtonsoft.Json;

namespace Bit.Core.Models.Data
{
    public class EmergencyAccessDetails : EmergencyAccess, ITwoFactorProvidersUser
    {
        private Dictionary<TwoFactorProviderType, TwoFactorProvider> _twoFactorProviders;

        public string GranteeName { get; set; }
        public string GranteeEmail { get; set; }
        public string GranteeTwoFactorProviders { get; set; }
        public bool? GranteePremium { get; set; }
        public string GrantorName { get; set; }
        public string GrantorEmail { get; set; }

        public string TwoFactorProviders
        {
            get { return GranteeTwoFactorProviders; }
        }

        public Dictionary<TwoFactorProviderType, TwoFactorProvider> GetTwoFactorProviders()
        {
            if (string.IsNullOrWhiteSpace(GranteeTwoFactorProviders))
            {
                return null;
            }

            try
            {
                if (_twoFactorProviders == null)
                {
                    _twoFactorProviders =
                        JsonConvert.DeserializeObject<Dictionary<TwoFactorProviderType, TwoFactorProvider>>(
                            GranteeTwoFactorProviders);
                }

                return _twoFactorProviders;
            }
            catch (JsonSerializationException)
            {
                return null;
            }
        }

        public Guid? GetUserId()
        {
            return GranteeId;
        }

        public bool GetPremium()
        {
            return GranteePremium.GetValueOrDefault(false);
        }
    }
}
