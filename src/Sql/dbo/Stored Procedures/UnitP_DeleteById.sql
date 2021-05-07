CREATE PROCEDURE [dbo].[UnitP_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[User_BumpAccountRevisionDateByUnitPId] @Id

    BEGIN TRANSACTION UnitP_DeleteById

    DELETE
    FROM
        [dbo].[OrganizationUser]
    WHERE
        [UnitPId] = @Id    
    
    DELETE
    FROM 
        [dbo].[UnitPUser]
    WHERE 
        [UnitPId] = @Id

    DELETE
    FROM
        [dbo].[UnitP]
    WHERE
        [Id] = @Id

    COMMIT TRANSACTION UnitP_DeleteById
END
