IF OBJECT_ID('[dbo].[Cipher_DeleteByIdsOrganizationId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Cipher_DeleteByIdsOrganizationId];
END
GO

IF OBJECT_ID('[dbo].[Cipher_SoftDeleteByIdsOrganizationId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Cipher_SoftDeleteByIdsOrganizationId];
END
GO

CREATE PROCEDURE [dbo].[Cipher_DeleteByIdsOrganizationId]
    @Ids AS [dbo].[GuidIdArray] READONLY,
    @OrganizationId AS UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    -- Delete ciphers
    DELETE
    FROM
        [dbo].[Cipher]
    WHERE
        [Id] IN (SELECT * FROM @Ids)
        AND OrganizationId = @OrganizationId

    -- Cleanup organization
    EXEC [dbo].[Organization_UpdateStorage] @OrganizationId
    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrganizationId
END
GO

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
GO
