using System;
using System.Collections.Generic;
using Bit.Core.Enums;
using Newtonsoft.Json;

namespace Bit.Core.Models.Data
{
    public class OrganizationUserUserDetails : IExternal, ITwoFactorProvidersUser
    {
        private Dictionary<TwoFactorProviderType, TwoFactorProvider> _twoFactorProviders;

        public Guid Id { get; set; }
        public Guid OrganizationId { get; set; }
        public Guid? UserId { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string TwoFactorProviders { get; set; }
        public bool? Premium { get; set; }
        public OrganizationUserStatusType Status { get; set; }
        public OrganizationUserType Type { get; set; }
        public bool AccessAll { get; set; }
        public string ExternalId { get; set; }

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
            return UserId;
        }

        public bool GetPremium()
        {
            return Premium.GetValueOrDefault(false);
        }
    }
}
