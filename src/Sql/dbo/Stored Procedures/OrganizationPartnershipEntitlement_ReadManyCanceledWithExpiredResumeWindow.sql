CREATE PROCEDURE [dbo].[OrganizationPartnershipEntitlement_ReadManyCanceledWithExpiredResumeWindow]
    @AsOf DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- State 3 is PartnershipEntitlementState.Canceled
    SELECT
        *
    FROM
        [dbo].[OrganizationPartnershipEntitlementView]
    WHERE
        [State] = 3
        AND [UserId] IS NOT NULL
        AND [ResumeWindowExpirationDate] <= @AsOf
END
