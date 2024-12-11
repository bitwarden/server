using System.Diagnostics.CodeAnalysis;
using Bit.Core.Entities;

namespace Bit.Infrastructure.EFIntegration.Test.Repositories.EqualityComparers;

public class OrganizationSponsorshipCompare : IEqualityComparer<OrganizationSponsorship>
{
    public bool Equals(OrganizationSponsorship x, OrganizationSponsorship y)
    {
        return x.SponsoringOrganizationId.Equals(y.SponsoringOrganizationId)
            && x.SponsoringOrganizationUserId.Equals(y.SponsoringOrganizationUserId)
            && x.SponsoredOrganizationId.Equals(y.SponsoredOrganizationId)
            && x.OfferedToEmail.Equals(y.OfferedToEmail)
            && x.ToDelete.Equals(y.ToDelete)
            && x.ValidUntil.ToString().Equals(y.ValidUntil.ToString());
    }

    public int GetHashCode([DisallowNull] OrganizationSponsorship obj)
    {
        return base.GetHashCode();
    }
}
