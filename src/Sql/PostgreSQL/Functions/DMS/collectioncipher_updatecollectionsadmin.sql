CREATE OR REPLACE PROCEDURE vault_dbo.collectioncipher_updatecollectionsadmin(par_cipherid uuid, par_organizationid uuid, par_collectionids vault_dbo.guididarray)
 LANGUAGE plpgsql
AS $procedure$
/*
[7916 - Severity CRITICAL - Current MERGE statement can not be emulated by INSERT ON CONFLICT usage. To achieve the effect of a MERGE statement, use separate INSERT, DELETE, and UPDATE statements or by cursor usage.]
BEGIN
    SET NOCOUNT ON

    ;WITH [AvailableCollectionsCTE] AS(
        SELECT
            Id
        FROM
            [dbo].[Collection]
        WHERE
            OrganizationId = @OrganizationId
    ),
    [CollectionCiphersCTE] AS(
        SELECT
            [CollectionId],
            [CipherId]
        FROM
            [dbo].[CollectionCipher]
        WHERE
            [CipherId] = @CipherId
    )
    MERGE
        [CollectionCiphersCTE] AS [Target]
    USING
        @CollectionIds AS [Source]
    ON
        [Target].[CollectionId] = [Source].[Id]
        AND [Target].[CipherId] = @CipherId
    WHEN NOT MATCHED BY TARGET
    AND [Source].[Id] IN (SELECT [Id] FROM [AvailableCollectionsCTE]) THEN
        INSERT VALUES
        (
            [Source].[Id],
            @CipherId
        )
    WHEN NOT MATCHED BY SOURCE
    AND [Target].[CipherId] = @CipherId THEN
        DELETE
    ;

    IF @OrganizationId IS NOT NULL
    BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrganizationId
    END
END
*/
BEGIN
END;
$procedure$
;
