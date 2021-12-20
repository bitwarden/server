using System.Linq;
using Bit.Core.Enums;
using Bit.Core.Models.Table;

namespace Bit.Core.Models.Business
{
    public class OrganizationUpgrade
    {
        public string BusinessName { get; set; }
        public PlanType Plan { get; set; }
        public int AdditionalSeats { get; set; }
        public short AdditionalStorageGb { get; set; }
        public bool PremiumAccessAddon { get; set; }
        public TaxInfo TaxInfo { get; set; }
        public string PublicKey { get; set; }
        public string PrivateKey { get; set; }

        /// <summary>
        /// Creates and new clone of organization and applies this upgrade to it.
        /// </summary>
        /// <param name="organization"></param>
        /// <param name="enable"></param>
        /// <returns></returns>
        public Organization ApplyToOrganization(Organization organization, bool enable)
        {
            var newOrg = organization.Clone();
            var newPlan = Utilities.StaticStore.Plans.FirstOrDefault(p => p.Type == Plan && !p.Disabled);

            organization.BusinessName = BusinessName;
            organization.PlanType = newPlan.Type;
            organization.Seats = (short)(newPlan.BaseSeats + AdditionalSeats);
            organization.MaxCollections = newPlan.MaxCollections;
            organization.UseGroups = newPlan.HasGroups;
            organization.UseDirectory = newPlan.HasDirectory;
            organization.UseEvents = newPlan.HasEvents;
            organization.UseTotp = newPlan.HasTotp;
            organization.Use2fa = newPlan.Has2fa;
            organization.UseApi = newPlan.HasApi;
            organization.SelfHost = newPlan.HasSelfHost;
            organization.UsePolicies = newPlan.HasPolicies;
            organization.MaxStorageGb = !newPlan.BaseStorageGb.HasValue ?
                (short?)null :
                (short)(newPlan.BaseStorageGb.Value + AdditionalStorageGb);
            organization.UseGroups = newPlan.HasGroups;
            organization.UseDirectory = newPlan.HasDirectory;
            organization.UseEvents = newPlan.HasEvents;
            organization.UseTotp = newPlan.HasTotp;
            organization.Use2fa = newPlan.Has2fa;
            organization.UseApi = newPlan.HasApi;
            organization.UseSso = newPlan.HasSso;
            organization.UseKeyConnector = newPlan.HasKeyConnector;
            organization.UseResetPassword = newPlan.HasResetPassword;
            organization.SelfHost = newPlan.HasSelfHost;
            organization.UsersGetPremium = newPlan.UsersGetPremium || PremiumAccessAddon;
            organization.Plan = newPlan.Name;
            organization.Enabled = enable;
            organization.PublicKey = PublicKey;
            organization.PrivateKey = PrivateKey;

            return newOrg;
        }
    }
}
