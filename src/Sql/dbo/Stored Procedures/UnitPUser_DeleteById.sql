CREATE PROCEDURE [dbo].[UnitPUser_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[User_BumpAccountRevisionDateByUnitPUserId] @Id

    BEGIN TRANSACTION UnitPUser_DeleteById

    DECLARE @UnitPId UNIQUEIDENTIFIER
    DECLARE @UserId UNIQUEIDENTIFIER

    SELECT
        @UnitPId = [UnitPId],
        @UserId = [UserId]
    FROM
        [dbo].[UnitPUser]
    WHERE
        [Id] = @Id

    DELETE
    FROM
         [dbo].[OrganizationUser]
    WHERE
        [UnitPId] = @UnitPId
    AND
        [UserId] = @UserId

    DELETE
    FROM
        [dbo].[UnitPUser]
    WHERE
        [Id] = @Id

    COMMIT TRANSACTION UnitPUser_DeleteById
END
