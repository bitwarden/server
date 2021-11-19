using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Bit.Core.Models.Table;

namespace Bit.Core.Test.Repositories.EntityFramework.EqualityComparers
{
    public class OrganizationSponsorshipCompare : IEqualityComparer<OrganizationSponsorship>
    {
        public bool Equals(OrganizationSponsorship x, OrganizationSponsorship y)
        {
            return x.InstallationId.Equals(y.InstallationId) &&
                x.SponsoringOrganizationId.Equals(y.SponsoringOrganizationId) &&
                x.SponsoringOrganizationUserId.Equals(y.SponsoringOrganizationUserId) &&
                x.SponsoredOrganizationId.Equals(y.SponsoredOrganizationId) &&
                x.OfferedToEmail.Equals(y.OfferedToEmail) &&
                x.CloudSponsor.Equals(y.CloudSponsor) &&
                x.TimesRenewedWithoutValidation.Equals(y.TimesRenewedWithoutValidation) &&
                x.SponsorshipLapsedDate.ToString().Equals(y.SponsorshipLapsedDate.ToString());
        }

        public int GetHashCode([DisallowNull] OrganizationSponsorship obj)
        {
            return base.GetHashCode();
        }
    }
}
