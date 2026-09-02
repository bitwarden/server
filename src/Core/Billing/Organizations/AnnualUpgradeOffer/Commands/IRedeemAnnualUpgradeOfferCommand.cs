using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Commands;
using OneOf.Types;

namespace Bit.Core.Billing.Organizations.AnnualUpgradeOffer.Commands;

/// <summary>
/// Switches an organization from a monthly plan to the annual-latest plan for its product tier,
/// effective at next renewal, by creating a two-phase Stripe subscription schedule. If an active
/// schedule already exists (e.g. a Track A price-migration schedule), it is released first --
/// the organization migrates straight to the annual-latest plan instead.
/// </summary>
public interface IRedeemAnnualUpgradeOfferCommand
{
    Task<BillingCommandResult<None>> Run(Organization organization);
}
