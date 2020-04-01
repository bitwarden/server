CREATE PROCEDURE [dbo].[Cipher_RestoreById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @UserId UNIQUEIDENTIFIER
    DECLARE @OrganizationId UNIQUEIDENTIFIER

    SELECT TOP 1
        @UserId = [UserId],
        @OrganizationId = [OrganizationId]
    FROM
        [dbo].[Cipher]
    WHERE
        [Id] = @Id
	
    UPDATE
        [dbo].[Cipher]
    SET
        [DeletedDate] = NULL,
        [RevisionDate] = GETUTCDATE()
    WHERE
        [Id] = @Id

    IF @OrganizationId IS NOT NULL
    BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDateByCipherId] @Id, @OrganizationId
    END
    ELSE IF @UserId IS NOT NULL
    BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
    END
END