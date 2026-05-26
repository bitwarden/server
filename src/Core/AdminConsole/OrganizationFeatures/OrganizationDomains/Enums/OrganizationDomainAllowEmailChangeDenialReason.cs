namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationDomains.Enums;

public enum OrganizationDomainAllowEmailChangeDenialReason
{
    Allowed,
    UserIsClaimedAndDomainNotVerified,
    DomainIsBlockedByPolicy,
}