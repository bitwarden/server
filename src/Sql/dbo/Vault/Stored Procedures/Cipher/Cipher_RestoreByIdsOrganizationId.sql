CREATE PROCEDURE [dbo].[Cipher_RestoreByIdsOrganizationId]
    @Ids AS [dbo].[GuidIdArray] READONLY,
    @OrganizationId AS UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON
        
    IF (SELECT COUNT(1) FROM @Ids) < 1
        BEGIN
            RETURN(-1)
        END

    -- Delete ciphers
    DECLARE @UtcNow DATETIME2(7) = GETUTCDATE();
    UPDATE
        [dbo].[Cipher]
    SET
        [DeletedDate] = NULL,
        [RevisionDate] = @UtcNow
    WHERE
        [Id] IN (SELECT * FROM @Ids)
        AND OrganizationId = @OrganizationId

    -- Cleanup organization
    EXEC [dbo].[Organization_UpdateStorage] @OrganizationId
    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrganizationId

    SELECT @UtcNow
END