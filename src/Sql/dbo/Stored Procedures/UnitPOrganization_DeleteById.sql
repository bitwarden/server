CREATE PROCEDURE [dbo].[UnitPOrganization_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[User_BumpAccountRevisionDateByUnitPOrganizationId] @Id

    BEGIN TRANSACTION UnitPOrganization_DeleteById

    DECLARE @UnitPId UNIQUEIDENTIFIER
    DECLARE @OrganizationId UNIQUEIDENTIFIER

    SELECT
        @UnitPId = [UnitPId],
        @OrganizationId = [OrganizationId]
    FROM
        [dbo].[UnitPOrganization]
    WHERE
        [Id] = @Id

    DELETE
    FROM
        [dbo].[OrganizationUser]
    WHERE
        [UnitPId] = @UnitPId
    AND
        [OrganizationId] = @OrganizationId
    
    DELETE
    FROM
        [dbo].[UnitPOrganization]
    WHERE
        [Id] = @Id

    COMMIT TRANSACTION UnitPOrganization_DeleteById
END
