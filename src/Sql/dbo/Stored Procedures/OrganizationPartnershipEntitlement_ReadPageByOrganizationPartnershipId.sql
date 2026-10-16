CREATE PROCEDURE [dbo].[OrganizationPartnershipEntitlement_ReadPageByOrganizationPartnershipId]
    @OrganizationPartnershipId UNIQUEIDENTIFIER,
    @AfterId                   UNIQUEIDENTIFIER,
    @PageSize                  INT
AS
BEGIN
    SET NOCOUNT ON

    SELECT TOP (@PageSize)
        *
    FROM
        [dbo].[OrganizationPartnershipEntitlementView]
    WHERE
        [OrganizationPartnershipId] = @OrganizationPartnershipId
        AND (@AfterId IS NULL OR [Id] > @AfterId)
    ORDER BY
        [Id] ASC
END
