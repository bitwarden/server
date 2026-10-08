CREATE PROCEDURE [dbo].[OrganizationPartnershipEntitlement_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[OrganizationPartnershipEntitlement]
    WHERE
        [Id] = @Id
END
