CREATE VIEW [dbo].[PlanTypePlanTypeGroup]
AS
SELECT
    PT.Id,
    PT.StripePlanId,
    PT.StripeSeatPlanId,
    PT.StripeStoragePlanId,
    PT.StripePremiumAccessPlanId,
    PT.BasePrice,
    PT.SeatPrice,
    PT.AdditionalStoragePricePerGb,
    PT.HasPremiumAccessAddonCost,
    PT.IsAnnual,
    PTG.Id AS PlanTypeGroupId,
    PTG.Name,
    PTG.Description,
    PTG.CanBeUsedByBusiness,
    PTG.BaseSeats,
    PTG.BaseStorageGb,
    PTG.MaxCollections,
    PTG.HasAdditionalSeatsOption,
    PTG.HasAdditionalStorageOption,
    PTG.HasPremiumAccessAddon,
    PTG.TrialPeriodDays,
    PTG.HasSelfHost,
    PTG.HasPolicies,
    PTG.HasGroups,
    PTG.HasDirectory,
    PTG.HasEvents,
    PTG.HasTotp,
    PTG.Has2fa,
    PTG.HasApi,
    PTG.UsersGetPremium,
    PTG.HasSso,
    PTG.SortOrder,
    PTG.IsLegacy
FROM
    [dbo].[PlanType] PT
INNER JOIN
    [dbo].[PlanTypeGroup] PTG ON PTG.[Id] = PT.[PlanTypeGroupId]
