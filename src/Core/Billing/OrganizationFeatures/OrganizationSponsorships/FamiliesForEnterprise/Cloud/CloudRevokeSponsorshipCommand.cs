﻿using Bit.Core.Billing.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.Billing.Repositories;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;

namespace Bit.Core.Billing.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Cloud;

public class CloudRevokeSponsorshipCommand : CancelSponsorshipCommand, IRevokeSponsorshipCommand
{
    public CloudRevokeSponsorshipCommand(
        IOrganizationSponsorshipRepository organizationSponsorshipRepository,
        IOrganizationRepository organizationRepository) : base(organizationSponsorshipRepository, organizationRepository)
    {
    }

    public async Task RevokeSponsorshipAsync(OrganizationSponsorship sponsorship)
    {
        if (sponsorship == null)
        {
            throw new BadRequestException("You are not currently sponsoring an organization.");
        }

        if (sponsorship.SponsoredOrganizationId == null)
        {
            await base.DeleteSponsorshipAsync(sponsorship);
        }
        else
        {
            await MarkToDeleteSponsorshipAsync(sponsorship);
        }
    }
}
