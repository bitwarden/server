CREATE PROCEDURE [dbo].[Send_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @UserId UNIQUEIDENTIFIER
    DECLARE @OrganizationId UNIQUEIDENTIFIER
    DECLARE @Type TINYINT

    SELECT TOP 1
        @UserId = [UserId],
        @OrganizationId = [OrganizationId],
        @Type = [Type]
    FROM
        [dbo].[Send]
    WHERE
        [Id] = @Id

    DELETE
    FROM
        [dbo].[Send]
    WHERE
        [Id] = @Id

    IF @UserId IS NOT NULL
    BEGIN
        IF @Type = 1 --File
        BEGIN
            EXEC [dbo].[User_UpdateStorage] @UserId
        END
        EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
    END
    -- TODO: OrganizationId bump?
END