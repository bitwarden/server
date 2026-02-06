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