using System;
using System.Collections.Generic;
using Bit.Core.Enums;
using Bit.Core.Models.Interfaces;
using Newtonsoft.Json;

namespace Bit.Core.Models.Data
{
    public class OrganizationUserUserDetails : IExternal, ITwoFactorProvidersUser, IPermissions
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
        public string SsoExternalId { get; set; }
        public bool AccessBusinessPortal { get; set; }
        public bool AccessEventLogs { get; set; }
        public bool AccessImportExport { get; set; }
        public bool AccessReports { get; set; }
        public bool ManageAllCollections { get; set; }
        public bool ManageAssignedCollections { get; set; }
        public bool ManageGroups { get; set; }
        public bool ManagePolicies { get; set; }
        public bool ManageUsers { get; set; }

        public Dictionary<TwoFactorProviderType, TwoFactorProvider> GetTwoFactorProviders()
        {
            if (string.IsNullOrWhiteSpace(TwoFactorProviders))
            {
                return null;
            }

            try
            {
                if (_twoFactorProviders == null)
                {
                    _twoFactorProviders =
                        JsonConvert.DeserializeObject<Dictionary<TwoFactorProviderType, TwoFactorProvider>>(
                            TwoFactorProviders);
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
            return UserId;
        }

        public bool GetPremium()
        {
            return Premium.GetValueOrDefault(false);
        }
    }
}
