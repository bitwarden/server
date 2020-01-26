CREATE OR REPLACE PROCEDURE vault_dbo.collectionuser_updateusers(par_collectionid uuid, par_users vault_dbo.selectionreadonlyarray)
 LANGUAGE plpgsql
AS $procedure$
/*
[7916 - Severity CRITICAL - Current MERGE statement can not be emulated by INSERT ON CONFLICT usage. To achieve the effect of a MERGE statement, use separate INSERT, DELETE, and UPDATE statements or by cursor usage.]
BEGIN
    SET NOCOUNT ON

    DECLARE @OrgId UNIQUEIDENTIFIER = (
        SELECT TOP 1
            [OrganizationId]
        FROM
            [dbo].[Collection]
        WHERE
            [Id] = @CollectionId
    )

    ;WITH [AvailableUsersCTE] AS(
        SELECT
            Id
        FROM
            [dbo].[OrganizationUser]
        WHERE
            OrganizationId = @OrgId
    )
    MERGE
        [dbo].[CollectionUser] AS [Target]
    USING
        @Users AS [Source]
    ON
        [Target].[CollectionId] = @CollectionId
        AND [Target].[OrganizationUserId] = [Source].[Id]
    WHEN NOT MATCHED BY TARGET
    AND [Source].[Id] IN (SELECT [Id] FROM [AvailableUsersCTE]) THEN
        INSERT VALUES
        (
            @CollectionId,
            [Source].[Id],
            [Source].[ReadOnly]
        )
    WHEN MATCHED AND [Target].[ReadOnly] != [Source].[ReadOnly] THEN
        UPDATE SET [Target].[ReadOnly] = [Source].[ReadOnly]
    WHEN NOT MATCHED BY SOURCE
    AND [Target].[CollectionId] = @CollectionId THEN
        DELETE
    ;

    EXEC [dbo].[User_BumpAccountRevisionDateByCollectionId] @CollectionId, @OrgId
END
*/
BEGIN
END;
$procedure$
;
