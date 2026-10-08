CREATE PROCEDURE [dbo].[OrganizationPartnership_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    BEGIN TRANSACTION OrgPartnership_DeleteById

    DELETE
    FROM
        [dbo].[OrganizationPartnershipEntitlement]
    WHERE
        [OrganizationPartnershipId] = @Id

    DELETE
    FROM
        [dbo].[OrganizationPartnership]
    WHERE
        [Id] = @Id

    COMMIT TRANSACTION OrgPartnership_DeleteById
END
