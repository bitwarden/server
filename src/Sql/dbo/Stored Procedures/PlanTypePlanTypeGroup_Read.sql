CREATE PROCEDURE [dbo].[PlanTypePlanTypeGroup_Read]
AS
BEGIN
    SELECT
        Id,
        StripePlanId,
        StripeSeatPlanId,
        StripeStoragePlanId,
        StripePremiumAccessPlanId,
        BasePrice,
        SeatPrice,
        AdditionalStoragePricePerGb,
        HasPremiumAccessAddonCost,
        IsAnnual,
        PlanTypeGroupId AS "PlanTypeGroup.Id",
        Name AS "PlanTypeGroup.Name",
        Description AS "PlanTypeGroup.Description",
        CanBeUsedByBusiness AS "PlanTypeGroup.CanBeUsedByBusiness",
        BaseSeats AS "PlanTypeGroup.BaseSeats",
        BaseStorageGb AS "PlanTypeGroup.BaseStorageGb",
        MaxCollections AS "PlanTypeGroup.MaxCollections",
        HasAdditionalSeatsOption AS "PlanTypeGroup.HasAdditionalSeatsOption",
        HasAdditionalStorageOption AS "PlanTypeGroup.HasAdditionalStorageOption",
        HasPremiumAccessAddon AS "PlanTypeGroup.HasPremiumAccessAddon",
        TrialPeriodDays AS "PlanTypeGroup.TrialPeriodDays",
        HasSelfHost AS "PlanTypeGroup.HasSelfHost",
        HasPolicies AS "PlanTypeGroup.HasPolicies",
        HasGroups AS "PlanTypeGroup.HasGroups",
        HasDirectory AS "PlanTypeGroup.HasDirectory",
        HasEvents AS "PlanTypeGroup.HasEvents",
        HasTotp AS "PlanTypeGroup.HasTotp",
        Has2fa AS "PlanTypeGroup.Has2fa",
        HasApi AS "PlanTypeGroup.HasApi",
        UsersGetPremium AS "PlanTypeGroup.UsersGetPremium",
        HasSso AS "PlanTypeGroup.HasSso",
        SortOrder AS "PlanTypeGroup.SortOrder",
        IsLegacy AS "PlanTypeGroup.IsLegacy"
    FROM [dbo].[PlanTypePlanTypeGroup]
    FOR JSON PATH
END
