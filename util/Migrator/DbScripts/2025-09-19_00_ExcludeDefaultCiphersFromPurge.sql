CREATE OR ALTER PROCEDURE [dbo].[Cipher_DeleteByOrganizationId]
    @OrganizationId AS UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @BatchSize INT = 1000;

    BEGIN TRY
        BEGIN TRANSACTION Cipher_DeleteByOrganizationId;

        WHILE @BatchSize > 0
        BEGIN
            DELETE
            FROM
                [dbo].[Cipher]
            WHERE
                Id IN
                (SELECT TOP (@BatchSize)
                        C.Id
                    FROM
                        [dbo].[Cipher] C
                    LEFT JOIN
                        [dbo].[CollectionCipher] CC ON CC.[CipherId] = C.[Id]
                    LEFT JOIN
                        [dbo].[Collection] Col ON Col.[Id] = CC.[CollectionId] AND Col.[Type] = 1
                    WHERE
                        C.[OrganizationId] = @OrganizationId
                    GROUP BY
                        C.[Id]
                    HAVING
                        MAX(Col.[Id]) IS NULL
                    ORDER BY
                        C.[Id]
                );

            SET @BatchSize = @@ROWCOUNT;
        END

        -- Clean up remaining CollectionCipher relationships for Type = 0 collections
        -- (for ciphers that were preserved because they're in both Typ1 in Type = 1 collections)
        SET @BatchSize = 1000;
        WHILE @BatchSize > 0
        BEGIN
            DELETE TOP (@BatchSize) CC
            FROM [dbo].[CollectionCipher] CC
            INNER JOIN [dbo].[Collection] Col ON Col.[Id] = CC.[CollectionId] AND Col.[Type] = 0
            INNER JOIN [dbo].[Cipher] C ON C.[Id] = CC.[CipherId]
            WHERE C.[OrganizationId] = @OrganizationId;

            SET @BatchSize = @@ROWCOUNT;
        END

        EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrganizationId;
        COMMIT TRANSACTION Cipher_DeleteByOrganizationId;

        EXEC [dbo].[Organization_UpdateStorage] @OrganizationId;

    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION Cipher_DeleteByOrganizationId;
        THROW; -- Re-raise the error
    END CATCH
END
GO
