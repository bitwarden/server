CREATE PROCEDURE [dbo].[OrganizationPartnershipEntitlement_ReadByOrganizationPartnershipIdExternalIdHash]
    @OrganizationPartnershipId UNIQUEIDENTIFIER,
    @ExternalIdHash            VARCHAR(64)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationPartnershipEntitlementView]
    WHERE
        [OrganizationPartnershipId] = @OrganizationPartnershipId
        AND [ExternalIdHash] = @ExternalIdHash
END
