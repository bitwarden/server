CREATE OR REPLACE PROCEDURE group_updatewithcollections(par_id uuid, par_organizationid uuid, par_name character varying, par_accessall numeric, par_externalid character varying, par_creationdate timestamp without time zone, par_revisiondate timestamp without time zone, par_collections selectionreadonlyarray)
 LANGUAGE plpgsql
AS $procedure$
/*
[7916 - Severity CRITICAL - Current MERGE statement can not be emulated by INSERT ON CONFLICT usage. To achieve the effect of a MERGE statement, use separate INSERT, DELETE, and UPDATE statements or by cursor usage.]
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[Group_Update] @Id, @OrganizationId, @Name, @AccessAll, @ExternalId, @CreationDate, @RevisionDate

    ;WITH [AvailableCollectionsCTE] AS(
        SELECT
            Id
        FROM
            [dbo].[Collection]
        WHERE
            OrganizationId = @OrganizationId
    )
    MERGE
        [dbo].[CollectionGroup] AS [Target]
    USING
        @Collections AS [Source]
    ON
        [Target].[CollectionId] = [Source].[Id]
        AND [Target].[GroupId] = @Id
    WHEN NOT MATCHED BY TARGET
    AND [Source].[Id] IN (SELECT [Id] FROM [AvailableCollectionsCTE]) THEN
        INSERT VALUES
        (
            [Source].[Id],
            @Id,
            [Source].[ReadOnly]
        )
    WHEN MATCHED AND [Target].[ReadOnly] != [Source].[ReadOnly] THEN
        UPDATE SET [Target].[ReadOnly] = [Source].[ReadOnly]
    WHEN NOT MATCHED BY SOURCE
    AND [Target].[GroupId] = @Id THEN
        DELETE
    ;

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrganizationId
END
*/
BEGIN
END;
$procedure$
;
