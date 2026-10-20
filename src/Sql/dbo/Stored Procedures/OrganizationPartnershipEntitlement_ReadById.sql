CREATE PROCEDURE [dbo].[OrganizationPartnershipEntitlement_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationPartnershipEntitlementView]
    WHERE
        [Id] = @Id
END
