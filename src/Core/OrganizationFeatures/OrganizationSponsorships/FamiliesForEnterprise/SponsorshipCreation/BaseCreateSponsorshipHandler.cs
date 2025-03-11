using Bit.Core.Entities;

namespace Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.SponsorshipCreation;

public abstract class BaseCreateSponsorshipHandler
{
    private BaseCreateSponsorshipHandler _next;

    public BaseCreateSponsorshipHandler SetNext(BaseCreateSponsorshipHandler next)
    {
        _next = next;
        return next;
    }

    public virtual async Task<OrganizationSponsorship> HandleAsync(CreateSponsorshipRequest request)
    {
        if (_next != null)
        {
            return await _next.HandleAsync(request);
        }
        return null;
    }
}
