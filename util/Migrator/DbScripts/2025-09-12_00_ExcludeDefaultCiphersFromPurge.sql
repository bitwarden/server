CREATE OR ALTER PROCEDURE [dbo].[Cipher_DeleteByOrganizationId]
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
            -- Exclude ciphers in "Default" collection
            AND C.[Type] != 1

        SET @BatchSize = @@ROWCOUNT

        COMMIT TRANSACTION Cipher_DeleteByOrganizationId_CC
    END

    -- Reset batch size
    SET @BatchSize = 100

    -- Delete ciphers
    WHILE @BatchSize > 0
    BEGIN
        BEGIN TRANSACTION Cipher_DeleteByOrganizationId

        DELETE TOP(@BatchSize) C
        FROM
            [dbo].[Cipher] C
        WHERE
            C.[OrganizationId] = @OrganizationId
            AND NOT EXISTS (
                SELECT 1
                FROM [dbo].[CollectionCipher] CC
                INNER JOIN [dbo].[Collection] Col ON Col.[Id] = CC.[CollectionId]
                WHERE CC.[CipherId] = C.[Id] AND Col.[Type] = 1
            )

        SET @BatchSize = @@ROWCOUNT

        COMMIT TRANSACTION Cipher_DeleteByOrganizationId
    END

    -- Cleanup organization
    EXEC [dbo].[Organization_UpdateStorage] @OrganizationId
    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrganizationId
END
GO
