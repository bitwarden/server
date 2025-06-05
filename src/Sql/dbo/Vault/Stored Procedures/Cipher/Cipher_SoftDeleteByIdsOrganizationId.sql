CREATE PROCEDURE [dbo].[Cipher_SoftDeleteByIdsOrganizationId]
    @Ids AS [dbo].[GuidIdArray] READONLY,
    @OrganizationId AS UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    -- Delete ciphers
    DECLARE @UtcNow DATETIME2(7) = GETUTCDATE();
    UPDATE
        [dbo].[Cipher]
    SET
        [DeletedDate] = @UtcNow,
        [RevisionDate] = @UtcNow
    WHERE
        [Id] IN (SELECT * FROM @Ids)
        AND OrganizationId = @OrganizationId

    -- Cleanup organization
    EXEC [dbo].[Organization_UpdateStorage] @OrganizationId
    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrganizationId
END