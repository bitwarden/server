CREATE PROCEDURE [dbo].[Cipher_DeleteByOrganizationId]
    @OrganizationId AS UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @BatchSize INT = 100

    -- Delete collection ciphers
    WHILE @BatchSize > 0
    BEGIN
        BEGIN TRANSACTION Cipher_DeleteByOrganizationId_CC

        DELETE TOP(@BatchSize) CC
        FROM
            [dbo].[CollectionCipher] CC
        INNER JOIN
            [dbo].[Collection] C ON C.[Id] = CC.[CollectionId]
        WHERE
            C.[OrganizationId] = @OrganizationId

        SET @BatchSize = @@ROWCOUNT

        COMMIT TRANSACTION Cipher_DeleteByOrganizationId_CC
    END

    -- Reset batch size
    SET @BatchSize = 100

    -- Delete ciphers
    WHILE @BatchSize > 0
    BEGIN
        BEGIN TRANSACTION Cipher_DeleteByOrganizationId

        DELETE TOP(@BatchSize)
        FROM
            [dbo].[Cipher]
        WHERE
            [OrganizationId] = @OrganizationId

        SET @BatchSize = @@ROWCOUNT

        COMMIT TRANSACTION Cipher_DeleteByOrganizationId
    END

    -- Cleanup organization
    EXEC [dbo].[Organization_UpdateStorage] @OrganizationId
    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrganizationId
END