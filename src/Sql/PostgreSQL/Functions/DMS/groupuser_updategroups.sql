CREATE OR REPLACE PROCEDURE groupuser_updategroups(par_organization_userid uuid, par_groupids guididarray)
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
            [dbo].[OrganizationUser]
        WHERE
            [Id] = @OrganizationUserId
    )

    ;WITH [AvailableGroupsCTE] AS(
        SELECT
            [Id]
        FROM
            [dbo].[Group]
        WHERE
            [OrganizationId] = @OrgId
    )
    MERGE
        [dbo].[GroupUser] AS [Target]
    USING
        @GroupIds AS [Source]
    ON
        [Target].[GroupId] = [Source].[Id]
        AND [Target].[OrganizationUserId] = @OrganizationUserId
    WHEN NOT MATCHED BY TARGET
    AND [Source].[Id] IN (SELECT [Id] FROM [AvailableGroupsCTE]) THEN
        INSERT VALUES
        (
            [Source].[Id],
            @OrganizationUserId
        )
    WHEN NOT MATCHED BY SOURCE
    AND [Target].[OrganizationUserId] = @OrganizationUserId
    AND [Target].[GroupId] IN (SELECT [Id] FROM [AvailableGroupsCTE]) THEN
        DELETE
    ;

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationUserId] @OrganizationUserId
END
*/
BEGIN
END;
$procedure$
;
