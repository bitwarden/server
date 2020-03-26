CREATE PROCEDURE [dbo].[Cipher_DeleteById]
    @Id UNIQUEIDENTIFIER,
    @Permanent AS BIT = 0
WITH RECOMPILE
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @UserId UNIQUEIDENTIFIER
    DECLARE @OrganizationId UNIQUEIDENTIFIER
    DECLARE @Attachments BIT

    SELECT TOP 1
        @UserId = [UserId],
        @OrganizationId = [OrganizationId],
        @Attachments = CASE WHEN [Attachments] IS NOT NULL THEN 1 ELSE 0 END
    FROM
        [dbo].[Cipher]
    WHERE
        [Id] = @Id
        
    IF @Permanent = 1
    BEGIN
        DELETE
        FROM
            [dbo].[Cipher]
        WHERE
            [Id] = @Id
    END
    ELSE
    BEGIN
        UPDATE
            [dbo].[Cipher]
        SET
            [DeletedDate] = SYSUTCDATETIME()
        WHERE
            [Id] = @Id
    END

    IF @OrganizationId IS NOT NULL
    BEGIN
        IF @Attachments = 1 AND @Permanent = 1
        BEGIN
            EXEC [dbo].[Organization_UpdateStorage] @OrganizationId
        END
        EXEC [dbo].[User_BumpAccountRevisionDateByCipherId] @Id, @OrganizationId
    END
    ELSE IF @UserId IS NOT NULL
    BEGIN
        IF @Attachments = 1 AND @Permanent = 1
        BEGIN
            EXEC [dbo].[User_UpdateStorage] @UserId
        END
        EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
    END
END